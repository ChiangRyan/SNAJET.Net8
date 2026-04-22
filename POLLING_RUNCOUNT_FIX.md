# RunCount 被轮询覆盖问题修复说明

## 问题描述

用户设置运转次数（RunCount）后，虽然显示"运转次数已设置"，但实际值很快又变回原数值。根本原因是：**轮询服务在写入命令发送后立即读取了设备的旧值，覆盖了刚设置的新值**。

## 时序问题分析

```
T0: 用户点击"设置运转次数" 设置为 100
   ↓
T1: SendModbusWriteRunCountCommandAsync() 发送 MQTT 写命令到 ESP32
   ↓
T2: UI 本地更新为 100，数据库更新为 100
   ↓
T3: ModbusPollingService 继续 3 秒轮询周期
   ↓
T4: ESP32 仍在处理写入操作，但轮询已发起读取命令
   ↓
T5: ESP32 读取响应返回，包含尚未更新的旧值（例如 50）
   ↓
T6: MainViewModel.HandleEsp32MqttMessagesAsync() 处理读取响应
   ↓
T7: UI 和数据库被旧值 50 覆盖 ❌
```

## 解决方案

### 1. 扩展 IPollingStateService 接口

添加了 `PausePollingAsync()` 方法，允许临时暂停轮询：

```csharp
/// <summary>
/// 暫時禁用輪詢指定毫秒數。此方法用於防止輪詢在寫入命令後立即覆蓋新值。
/// </summary>
/// <param name="durationMilliseconds">暫停輪詢的時間（毫秒）</param>
Task PausePollingAsync(int durationMilliseconds);
```

### 2. 实现 PollingStateService.PausePollingAsync()

该方法：
- 记录轮询暂停前的状态
- 如果轮询已启用，则暂停它
- 等待指定的时间
- 恢复轮询到暂停前的状态

```csharp
public async Task PausePollingAsync(int durationMilliseconds)
{
    _wasEnabledBeforePause = _isPollingEnabled;

    if (_isPollingEnabled)
    {
        DisablePolling();
        _logger.LogInformation("輪詢狀態服務：輪詢已暫停 {DurationMs} 毫秒（用於 Modbus 寫入操作）。", durationMilliseconds);
    }

    await Task.Delay(durationMilliseconds);

    if (_wasEnabledBeforePause)
    {
        EnablePolling();
        _logger.LogInformation("輪詢狀態服務：暫停結束，輪詢已恢復。");
    }
}
```

### 3. 修改 MainViewModel 写入方法

在以下两个方法中添加轮询暂停逻辑：

#### a) `SendModbusWriteCommandAsync()` - 通用写入命令

```csharp
await _mqttService.PublishAsync(commandTopic, jsonPayload);
_logger.LogInformation("已發送 Modbus Write 命令到 {Topic} (SlaveID: {SlaveId}): {Payload}", 
                       commandTopic, slaveId, jsonPayload);

// 發送寫入命令後，暫停輪詢以防止輪詢讀取覆蓋新寫入的值
_logger.LogInformation("發送寫入命令後，暫停輪詢 5 秒以防止輪詢讀取覆蓋新值。");
_ = _pollingStateService.PausePollingAsync(5000); // 暫停 5 秒（fire-and-forget）

return true;
```

#### b) `SendModbusWriteRunCountCommandAsync()` - RunCount 专用写入

```csharp
await _mqttService.PublishAsync(commandTopic, jsonPayload);
_logger.LogInformation("已成功發送 Modbus Write RunCount 命令到 {Topic} (SlaveID: {SlaveId}, RunCount: {RunCount})", 
                       commandTopic, slaveId, runCountValue);

// 發送寫入命令後，暫停輪詢以防止輪詢讀取覆蓋新寫入的值
_logger.LogInformation("發送 RunCount 寫入命令後，暫停輪詢 5 秒以防止輪詢讀取覆蓋新值。");
_ = _pollingStateService.PausePollingAsync(5000); // 暫停 5 秒（fire-and-forget）

return true;
```

## 新的时序流程

```
T0: 用户点击"设置运转次数" 设置为 100
   ↓
T1: SendModbusWriteRunCountCommandAsync() 发送 MQTT 写命令到 ESP32
   ↓
T2: UI 本地更新为 100，数据库更新为 100
   ↓
T3: 立即调用 PausePollingAsync(5000) ✓
   ↓
T4: ModbusPollingService 被暂停（禁用轮询）
   ↓
T5: ESP32 处理写入命令（需时 1-2 秒）
   ↓
T6: 5 秒等待期间，不会有轮询读取发生
   ↓
T7: 5 秒后，轮询恢复并读取新值 100 ✓
   ↓
T8: UI 和数据库保持正确的值 100 ✅
```

## 暂停时间考虑

- **5000 毫秒（5 秒）**：足够让 ESP32 完成写入操作（通常 1-2 秒）并确保网络往返时间
- 这个时间是保守估计，考虑到：
  - ESP32 处理 Modbus 写入的时间
  - MQTT 网络延迟
  - 数据库保存时间

## 代码变更文件

1. **Core/Interfaces/IPollingStateService.cs** - 接口定义
2. **Core/Services/PollingStateService.cs** - 实现类
3. **Core/ViewModels/MainViewModel.cs** - 写入命令方法

## 日志输出示例

用户会在输出窗口中看到：

```
已成功發送 Modbus Write RunCount 命令到 devices/ESP32_001/modbus/write/request (SlaveID: 1, RunCount: 100)
發送 RunCount 寫入命令後，暫停輪詢 5 秒以防止輪詢讀取覆蓋新值。
輪詢狀態服務：輪詢已暫停 5000 毫秒（用於 Modbus 寫入操作）。
...（5 秒等待）...
輪詢狀態服務：暫停結束，輪詢已恢復。
```

## 测试建议

1. 设置运转次数为新值（例如 100）
2. 观察 UI 是否显示新值
3. 等待 5 秒
4. 确认新值保持不变（不会被旧值覆盖）
5. 检查日志输出是否显示轮询暂停/恢复的消息
