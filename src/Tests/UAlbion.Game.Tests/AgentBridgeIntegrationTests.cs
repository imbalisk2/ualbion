using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UAlbion.Game.Tests;

[Trait("Category", "Integration")]
public class AgentBridgeIntegrationTests : IAsyncLifetime
{
    const int MaxStartupSeconds = 90;

    ClientWebSocket _ws;
    readonly byte[] _recvBuf = new byte[65536];
    Process _gameProcess;

    public async Task InitializeAsync()
    {
        _ws = new ClientWebSocket();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _ws.ConnectAsync(new Uri("ws://localhost:7399/agent"), cts.Token);
        }
        catch
        {
            _ws.Dispose();
            _ws = null;
        }

        if (_ws?.State != WebSocketState.Open)
        {
            StartGame();
            _ws = new ClientWebSocket();
            var deadline = DateTime.UtcNow.AddSeconds(MaxStartupSeconds);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    _ws = new ClientWebSocket();
                    await _ws.ConnectAsync(new Uri("ws://localhost:7399/agent"), cts.Token);
                    break;
                }
                catch
                {
                    _ws?.Dispose();
                    _ws = null;
                    await Task.Delay(500);
                }
            }

            if (_ws?.State != WebSocketState.Open)
            {
                if (_gameProcess != null && !_gameProcess.HasExited)
                    _gameProcess.Kill(entireProcessTree: true);
                throw new TimeoutException(
                    $"Game did not start within {MaxStartupSeconds}s. "
                    + "Check that assets are present in C:\\Repository\\UAlbion\\albion\\");
            }
        }

        // Initialize game state by teleporting to a known map
        var resp = await SendCommand("{\"cmd\":\"teleport\",\"map\":\"Map.TorontoBegin\",\"x\":31,\"y\":76}");
        if (!resp.GetProperty("ok").GetBoolean())
        {
            if (_gameProcess != null && !_gameProcess.HasExited)
                _gameProcess.Kill(entireProcessTree: true);
            throw new Exception($"Failed to initialize game state via teleport: {resp}");
        }
    }

    void StartGame()
    {
        var exe = @"C:\Repository\UAlbion\build\UAlbion\bin\Debug\net9.0\UAlbion.exe";
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = "-d3d --agent --agent-headless",
            WorkingDirectory = @"C:\Repository\UAlbion",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        _gameProcess = new Process { StartInfo = psi };
        _gameProcess.Start();
        _ = Task.Run(() => { try { _gameProcess.StandardOutput.ReadToEnd(); } catch { } });
        _ = Task.Run(() => { try { _gameProcess.StandardError.ReadToEnd(); } catch { } });
    }

    public async Task DisposeAsync()
    {
        if (_ws != null)
        {
            if (_ws.State == WebSocketState.Open)
                try { await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); }
                catch { }
            _ws.Dispose();
        }

        if (_gameProcess != null && !_gameProcess.HasExited)
        {
            try { _gameProcess.Kill(entireProcessTree: true); }
            catch { }
            _gameProcess.Dispose();
        }
    }

    void RequireGame()
    {
        if (_ws?.State != WebSocketState.Open)
            Assert.Fail("Game not running and could not be started automatically");
    }

    async Task<JsonElement> SendCommand(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        var result = await _ws.ReceiveAsync(_recvBuf, CancellationToken.None);
        var text = Encoding.UTF8.GetString(_recvBuf, 0, result.Count);
        return JsonSerializer.Deserialize<JsonElement>(text);
    }

    [Fact]
    public async Task Ping_ReturnsOk()
    {
        RequireGame();
        var resp = await SendCommand("{\"cmd\":\"ping\"}");
        Assert.True(resp.GetProperty("ok").GetBoolean());
        Assert.True(resp.TryGetProperty("result", out _));
    }

    [Fact]
    public async Task Ping_ReturnsPong()
    {
        RequireGame();
        var resp = await SendCommand("{\"cmd\":\"ping\"}");
        Assert.True(resp.GetProperty("ok").GetBoolean());
        var result = resp.GetProperty("result");
        Assert.True(result.TryGetProperty("pong", out var pong));
        Assert.True(pong.GetBoolean());
    }

    [Fact]
    public async Task GetMap_ReturnsMapId()
    {
        RequireGame();
        var resp = await SendCommand("{\"cmd\":\"get_map\"}");
        Assert.True(resp.GetProperty("ok").GetBoolean());
        var result = resp.GetProperty("result");
        Assert.True(result.TryGetProperty("map_id", out _));
    }

    [Fact]
    public async Task GetMaps_ReturnsMapList()
    {
        RequireGame();
        var resp = await SendCommand("{\"cmd\":\"get_maps\"}");
        Assert.True(resp.GetProperty("ok").GetBoolean());
        var result = resp.GetProperty("result");
        Assert.True(result.TryGetProperty("maps", out var maps));
        Assert.True(maps.GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetParty_ReturnsParty()
    {
        RequireGame();
        var resp = await SendCommand("{\"cmd\":\"get_party\"}");
        Assert.True(resp.GetProperty("ok").GetBoolean(),
            $"get_party failed: {resp}");
        var result = resp.GetProperty("result");
        Assert.True(result.TryGetProperty("members", out _));
    }

    [Fact]
    public async Task GetTime_ReturnsGameTime()
    {
        RequireGame();
        var resp = await SendCommand("{\"cmd\":\"get_time\"}");
        Assert.True(resp.GetProperty("ok").GetBoolean());
        var result = resp.GetProperty("result");
        Assert.True(result.TryGetProperty("hour", out _));
    }

    [Fact]
    public async Task GetNpcs_ReturnsNpcList()
    {
        RequireGame();
        var resp = await SendCommand("{\"cmd\":\"get_npcs\"}");
        Assert.True(resp.GetProperty("ok").GetBoolean(),
            $"get_npcs failed: {resp}");
        var result = resp.GetProperty("result");
        Assert.True(result.TryGetProperty("npcs", out _));
    }

    [Fact]
    public async Task InvalidJson_ReturnsParseError()
    {
        RequireGame();
        var bytes = Encoding.UTF8.GetBytes("{invalid}");
        await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        var result = await _ws.ReceiveAsync(_recvBuf, CancellationToken.None);
        var text = Encoding.UTF8.GetString(_recvBuf, 0, result.Count);
        var resp = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.False(resp.GetProperty("ok").GetBoolean());
        Assert.Equal("parse_error", resp.GetProperty("error").GetString());
    }

    [Fact]
    public async Task GetActiveItems_ReturnsOk()
    {
        RequireGame();
        var resp = await SendCommand("{\"cmd\":\"get_active_items\"}");
        Assert.True(resp.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task GetScene_ReturnsSceneId()
    {
        RequireGame();
        var resp = await SendCommand("{\"cmd\":\"get_scene\"}");
        Assert.True(resp.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Teleport_ValidParams_ReturnsOk()
    {
        RequireGame();
        var resp = await SendCommand("{\"cmd\":\"teleport\",\"map\":\"Map.TorontoBegin\",\"x\":31,\"y\":76}");
        Assert.True(resp.GetProperty("ok").GetBoolean(),
            $"Teleport failed: {resp}");
    }

    [Fact]
    public async Task Teleport_WithStringDirection_ReturnsOk()
    {
        RequireGame();
        var resp = await SendCommand("{\"cmd\":\"teleport\",\"map\":\"Map.TorontoBegin\",\"x\":31,\"y\":76,\"direction\":\"South\"}");
        Assert.True(resp.GetProperty("ok").GetBoolean(),
            $"Teleport with direction failed: {resp}");
    }

    [Fact]
    public async Task Teleport_WithIntDirection_ReturnsOk()
    {
        RequireGame();
        var resp = await SendCommand("{\"cmd\":\"teleport\",\"map\":\"Map.TorontoBegin\",\"x\":31,\"y\":76,\"direction\":2}");
        Assert.True(resp.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Teleport_MissingMap_ReturnsError()
    {
        RequireGame();
        var resp = await SendCommand("{\"cmd\":\"teleport\",\"x\":31,\"y\":76}");
        Assert.False(resp.GetProperty("ok").GetBoolean());
        Assert.Equal("missing_param", resp.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Teleport_MissingX_ReturnsError()
    {
        RequireGame();
        var resp = await SendCommand("{\"cmd\":\"teleport\",\"map\":\"Map.TorontoBegin\",\"y\":76}");
        Assert.False(resp.GetProperty("ok").GetBoolean());
        Assert.Equal("missing_param", resp.GetProperty("error").GetString());
    }

    [Fact]
    public async Task UnknownCommand_ReturnsError()
    {
        RequireGame();
        var resp = await SendCommand("{\"cmd\":\"nonexistent_command_xyz\"}");
        Assert.False(resp.GetProperty("ok").GetBoolean());
        Assert.Equal("unknown_command", resp.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PartyMoveW_MovesForward()
    {
        RequireGame();
        var teleResp = await SendCommand(
            "{\"cmd\":\"teleport\",\"map\":\"Map.TorontoBegin\",\"x\":31,\"y\":76,\"direction\":\"East\"}");
        Assert.True(teleResp.GetProperty("ok").GetBoolean(), $"teleport: {teleResp}");

        var sceneResp = await SendCommand("{\"cmd\":\"get_scene\"}");
        Assert.True(sceneResp.GetProperty("ok").GetBoolean());
        Assert.Equal("World3D",
            sceneResp.GetProperty("result").GetProperty("scene_id").GetString());

        var posResp = await SendCommand("{\"cmd\":\"get_position\"}");
        Assert.True(posResp.GetProperty("ok").GetBoolean());
        var pos = posResp.GetProperty("result");
        int startX = pos.GetProperty("x").GetInt32();
        int startY = pos.GetProperty("y").GetInt32();
        var dir = pos.GetProperty("direction").GetString();

        var actionResp = await SendCommand(
            "{\"cmd\":\"send_input_action\",\"action\":\"party_move 0 -1\"}");
        Assert.True(actionResp.GetProperty("ok").GetBoolean());
        await Task.Delay(800);

        var newPosResp = await SendCommand("{\"cmd\":\"get_position\"}");
        Assert.True(newPosResp.GetProperty("ok").GetBoolean());
        var newPos = newPosResp.GetProperty("result");
        int newX = newPos.GetProperty("x").GetInt32();
        int newY = newPos.GetProperty("y").GetInt32();

        bool moved = newX != startX || newY != startY;
        Assert.True(moved,
            $"W (0,-1) did not move from ({startX},{startY}) dir={dir}");
    }

    [Fact]
    public async Task PartyMoveS_MovesBackward()
    {
        RequireGame();
        var teleResp = await SendCommand(
            "{\"cmd\":\"teleport\",\"map\":\"Map.TorontoBegin\",\"x\":31,\"y\":76,\"direction\":\"East\"}");
        Assert.True(teleResp.GetProperty("ok").GetBoolean());

        var posResp = await SendCommand("{\"cmd\":\"get_position\"}");
        Assert.True(posResp.GetProperty("ok").GetBoolean());
        var pos = posResp.GetProperty("result");
        int startX = pos.GetProperty("x").GetInt32();
        int startY = pos.GetProperty("y").GetInt32();
        var dir = pos.GetProperty("direction").GetString();

        var actionResp = await SendCommand(
            "{\"cmd\":\"send_input_action\",\"action\":\"party_move 0 1\"}");
        Assert.True(actionResp.GetProperty("ok").GetBoolean());
        await Task.Delay(800);

        var newPosResp = await SendCommand("{\"cmd\":\"get_position\"}");
        Assert.True(newPosResp.GetProperty("ok").GetBoolean());
        var newPos = newPosResp.GetProperty("result");
        int newX = newPos.GetProperty("x").GetInt32();
        int newY = newPos.GetProperty("y").GetInt32();

        bool moved = newX != startX || newY != startY;
        Assert.True(moved,
            $"S (0,1) did not move from ({startX},{startY}) dir={dir}");
    }

    [Fact]
    public async Task PartyMoveA_MovesLeft()
    {
        RequireGame();
        var teleResp = await SendCommand(
            "{\"cmd\":\"teleport\",\"map\":\"Map.TorontoBegin\",\"x\":31,\"y\":76,\"direction\":\"East\"}");
        Assert.True(teleResp.GetProperty("ok").GetBoolean());

        var posResp = await SendCommand("{\"cmd\":\"get_position\"}");
        Assert.True(posResp.GetProperty("ok").GetBoolean());
        var pos = posResp.GetProperty("result");
        int startX = pos.GetProperty("x").GetInt32();
        int startY = pos.GetProperty("y").GetInt32();
        var dir = pos.GetProperty("direction").GetString();

        var actionResp = await SendCommand(
            "{\"cmd\":\"send_input_action\",\"action\":\"party_move -1 0\"}");
        Assert.True(actionResp.GetProperty("ok").GetBoolean());
        await Task.Delay(800);

        var newPosResp = await SendCommand("{\"cmd\":\"get_position\"}");
        Assert.True(newPosResp.GetProperty("ok").GetBoolean());
        var newPos = newPosResp.GetProperty("result");
        int newX = newPos.GetProperty("x").GetInt32();
        int newY = newPos.GetProperty("y").GetInt32();

        bool moved = newX != startX || newY != startY;
        Assert.True(moved,
            $"A (-1,0) did not move from ({startX},{startY}) dir={dir}");
    }

    [Fact]
    public async Task PartyMoveD_MovesRight()
    {
        RequireGame();
        var teleResp = await SendCommand(
            "{\"cmd\":\"teleport\",\"map\":\"Map.TorontoBegin\",\"x\":31,\"y\":76,\"direction\":\"East\"}");
        Assert.True(teleResp.GetProperty("ok").GetBoolean());

        var posResp = await SendCommand("{\"cmd\":\"get_position\"}");
        Assert.True(posResp.GetProperty("ok").GetBoolean());
        var pos = posResp.GetProperty("result");
        int startX = pos.GetProperty("x").GetInt32();
        int startY = pos.GetProperty("y").GetInt32();
        var dir = pos.GetProperty("direction").GetString();

        var actionResp = await SendCommand(
            "{\"cmd\":\"send_input_action\",\"action\":\"party_move 1 0\"}");
        Assert.True(actionResp.GetProperty("ok").GetBoolean());
        await Task.Delay(800);

        var newPosResp = await SendCommand("{\"cmd\":\"get_position\"}");
        Assert.True(newPosResp.GetProperty("ok").GetBoolean());
        var newPos = newPosResp.GetProperty("result");
        int newX = newPos.GetProperty("x").GetInt32();
        int newY = newPos.GetProperty("y").GetInt32();

        bool moved = newX != startX || newY != startY;
        Assert.True(moved,
            $"D (1,0) did not move from ({startX},{startY}) dir={dir}");
    }
}
