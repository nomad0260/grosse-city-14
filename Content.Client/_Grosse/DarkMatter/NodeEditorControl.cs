using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Resources;
using Content.Shared._Grosse.DarkMatter;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._Grosse.DarkMatter;

/// <summary>
/// A small node editor built directly on top of a UI Control. Nodes are dragged with the left
/// mouse; clicking an output then an input creates a typed connection. The actual execution is
/// still performed exclusively by the server.
/// </summary>
public sealed partial class NodeEditorControl : Robust.Client.UserInterface.Control
{
    [Dependency] private IResourceCache _cache = default!;

    private const float NodeWidth = 188f;
    private const float HeaderHeight = 30f;
    private const float RowHeight = 20f;
    private const float PortRadius = 5f;
    private const float MinZoom = 0.45f;
    private const float MaxZoom = 2.2f;

    private readonly Font _font;
    private readonly Font _smallFont;

    private EffectGraph _graph = new();
    private Vector2 _viewOffset;
    private float _viewScale = 1f;
    private bool _panning;
    private Vector2 _panStart;
    private Vector2 _panOrigin;
    private int _selectedNodeId = -1;
    private int _draggingNode = -1;
    private Vector2 _dragOffset;
    private (int Node, EffectPortInfo Port, bool Output)? _connectingFrom;
    private Vector2 _mousePosition;

    public event Action? GraphChanged;
    public event Action<int>? NodeSelected;
    public event Action<string>? StatusMessage;

    public EffectGraph Graph => _graph;
    public int SelectedNodeId => _selectedNodeId;

    public NodeEditorControl()
    {
        IoCManager.InjectDependencies(this);
        _font = _cache.GetStack("Regular", 12);
        _smallFont = _cache.GetStack("Regular", 10);
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
        MinSize = new Vector2(640f, 460f);
    }

    public void SetGraph(EffectGraph graph, int selectedNodeId = -1)
    {
        _graph = graph.Clone();
        _selectedNodeId = selectedNodeId;
        _draggingNode = -1;
        _panning = false;
        _connectingFrom = null;
        NodeSelected?.Invoke(_selectedNodeId);
    }

    public void AddNode(string operationId, Vector2? position = null)
    {
        var definition = EffectOperationCatalog.Get(operationId);
        if (definition == null)
            return;

        if (_graph.Nodes.Count >= EffectOperationCatalog.MaxNodes)
        {
            StatusMessage?.Invoke($"Достигнут лимит: {EffectOperationCatalog.MaxNodes} узлов.");
            return;
        }

        var id = _graph.NextNodeId();
        var node = new EffectGraphNode
        {
            Id = id,
            Operation = operationId,
            Position = position ?? FindFreePosition(),
        };

        foreach (var parameter in definition.Parameters)
            node.Parameters[parameter.Id] = parameter.Default;

        _graph.Nodes.Add(node);
        _selectedNodeId = id;
        NodeSelected?.Invoke(id);
        Changed();
    }

    public void RemoveSelectedNode()
    {
        if (_selectedNodeId < 0)
            return;

        _graph.RemoveNode(_selectedNodeId);
        _selectedNodeId = -1;
        _connectingFrom = null;
        NodeSelected?.Invoke(-1);
        Changed();
    }

    public void RemoveNode(int id)
    {
        _graph.RemoveNode(id);
        if (_draggingNode == id)
            _draggingNode = -1;

        if (_connectingFrom is { } source && source.Node == id)
            _connectingFrom = null;

        if (_selectedNodeId == id)
        {
            _selectedNodeId = -1;
            NodeSelected?.Invoke(-1);
        }

        Changed();
    }

    public void SetParameter(int nodeId, string parameter, float value)
    {
        var node = _graph.FindNode(nodeId);
        if (node == null)
            return;

        var definition = EffectOperationCatalog.Get(node.Operation)?.Parameters.FirstOrDefault(p => p.Id == parameter);
        if (definition == null)
            return;

        node.Parameters[parameter] = Math.Clamp(value, definition.Min, definition.Max);
        Changed();
    }

    private Vector2 FindFreePosition()
    {
        var position = new Vector2(40f, 40f);

        while (_graph.Nodes.Any(node => (node.Position - position).Length() < 90f))
            position += new Vector2(28f, 28f);

        return position;
    }

    private void Changed()
    {
        GraphChanged?.Invoke();
    }

    private static float NodeHeight(EffectOperationInfo definition)
    {
        return HeaderHeight + Math.Max(definition.Inputs.Length, definition.Outputs.Length) * RowHeight + 10f;
    }

    private Vector2 ToScreen(Vector2 graphPosition)
    {
        return graphPosition * _viewScale + _viewOffset;
    }

    private Vector2 ToGraph(Vector2 screenPosition)
    {
        return (screenPosition - _viewOffset) / _viewScale;
    }

    private Vector2 InputPortPosition(EffectGraphNode node, EffectOperationInfo definition, int index)
    {
        return ToScreen(node.Position + new Vector2(0f, HeaderHeight + 8f + index * RowHeight));
    }

    private Vector2 OutputPortPosition(EffectGraphNode node, EffectOperationInfo definition, int index)
    {
        return ToScreen(node.Position + new Vector2(NodeWidth, HeaderHeight + 8f + index * RowHeight));
    }

    private (EffectGraphNode Node, EffectPortInfo Port, bool Output)? FindPort(Vector2 point)
    {
        // The port is drawn with a radius scaled by zoom, so the hit radius must scale too.
        var hitRadius = PortRadius * _viewScale + 5f;

        foreach (var node in _graph.Nodes)
        {
            var definition = EffectOperationCatalog.Get(node.Operation);
            if (definition == null)
                continue;

            for (var i = 0; i < definition.Inputs.Length; i++)
            {
                if ((InputPortPosition(node, definition, i) - point).Length() <= hitRadius)
                    return (node, definition.Inputs[i], false);
            }

            for (var i = 0; i < definition.Outputs.Length; i++)
            {
                if ((OutputPortPosition(node, definition, i) - point).Length() <= hitRadius)
                    return (node, definition.Outputs[i], true);
            }
        }

        return null;
    }

    private EffectGraphNode? FindNode(Vector2 point)
    {
        for (var i = _graph.Nodes.Count - 1; i >= 0; i--)
        {
            var node = _graph.Nodes[i];
            var definition = EffectOperationCatalog.Get(node.Operation);
            if (definition == null)
                continue;

            var topLeft = ToScreen(node.Position);
            var size = new Vector2(NodeWidth, NodeHeight(definition)) * _viewScale;
            if (point.X >= topLeft.X && point.Y >= topLeft.Y &&
                point.X <= topLeft.X + size.X && point.Y <= topLeft.Y + size.Y)
                return node;
        }

        return null;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            var node = FindNode(args.RelativePosition);
            if (node != null)
                RemoveNode(node.Id);
            else
                _connectingFrom = null;

            return;
        }

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        var port = FindPort(args.RelativePosition);
        if (port is { } hit)
        {
            // A port press must never fall through to dragging the node or panning the canvas.
            _draggingNode = -1;
            _panning = false;

            if (_connectingFrom is { } source)
            {
                if (source.Output == hit.Output)
                {
                    // Same direction: restart the connection from the newly clicked port.
                    _connectingFrom = (hit.Node.Id, hit.Port, hit.Output);
                    StatusMessage?.Invoke(hit.Output
                        ? $"Выход {hit.Node.Id}.{hit.Port.Label} выбран. Нажмите на вход."
                        : $"Вход {hit.Node.Id}.{hit.Port.Label} выбран. Нажмите на выход.");
                    return;
                }

                if (source.Output)
                    TryConnect(source.Node, source.Port.Id, hit.Node.Id, hit.Port);
                else
                    TryConnect(hit.Node.Id, hit.Port.Id, source.Node, source.Port);

                _connectingFrom = null;
                return;
            }

            _connectingFrom = (hit.Node.Id, hit.Port, hit.Output);
            StatusMessage?.Invoke(hit.Output
                ? $"Выход {hit.Node.Id}.{hit.Port.Label} выбран. Нажмите на вход."
                : $"Вход {hit.Node.Id}.{hit.Port.Label} выбран. Нажмите на выход.");
            return;
        }

        var body = FindNode(args.RelativePosition);
        if (body != null)
        {
            _connectingFrom = null;
            _selectedNodeId = body.Id;
            _draggingNode = body.Id;
            _dragOffset = body.Position - ToGraph(args.RelativePosition);
            NodeSelected?.Invoke(body.Id);
            return;
        }

        _selectedNodeId = -1;
        _connectingFrom = null;
        _panning = true;
        _panStart = args.RelativePosition;
        _panOrigin = _viewOffset;
        NodeSelected?.Invoke(-1);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function == EngineKeyFunctions.UIClick)
        {
            _draggingNode = -1;
            _panning = false;
        }
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        var before = ToGraph(args.RelativePosition);
        var factor = args.Delta.Y > 0f ? 1.1f : 0.9f;
        _viewScale = Math.Clamp(_viewScale * factor, MinZoom, MaxZoom);
        _viewOffset = args.RelativePosition - before * _viewScale;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        _mousePosition = args.RelativePosition;

        if (_panning)
        {
            _viewOffset = _panOrigin + (args.RelativePosition - _panStart);
            return;
        }

        if (_draggingNode >= 0 && _graph.FindNode(_draggingNode) is { } node)
        {
            var position = ToGraph(args.RelativePosition) + _dragOffset;
            node.Position = new Vector2(MathF.Max(0f, position.X), MathF.Max(0f, position.Y));
            Changed();
        }

    }

    private void TryConnect(int fromNodeId, string fromPort, int toNodeId, EffectPortInfo toPort)
    {
        var fromNode = _graph.FindNode(fromNodeId);
        var toNode = _graph.FindNode(toNodeId);
        var fromDefinition = fromNode == null ? null : EffectOperationCatalog.Get(fromNode.Operation);
        var toDefinition = toNode == null ? null : EffectOperationCatalog.Get(toNode.Operation);
        var fromPortInfo = fromDefinition?.FindOutput(fromPort);

        if (fromNode == null || toNode == null || fromPortInfo == null || toDefinition == null)
            return;

        if (fromPortInfo.Type != toPort.Type)
        {
            StatusMessage?.Invoke($"Несовместимые типы: {fromPortInfo.Label} → {toPort.Label}.");
            return;
        }

        if (fromNode.Id == toNode.Id)
        {
            StatusMessage?.Invoke("Нельзя соединить узел с самим собой.");
            return;
        }

        if (HasPath(toNode.Id, fromNode.Id))
        {
            StatusMessage?.Invoke("Связь создаст цикл. Разорвите существующий путь.");
            return;
        }

        var replacesInput = _graph.Connections.Any(connection =>
            connection.ToNode == toNode.Id && connection.ToPort == toPort.Id);
        if (!replacesInput && _graph.Connections.Count >= EffectOperationCatalog.MaxConnections)
        {
            StatusMessage?.Invoke($"Достигнут лимит: {EffectOperationCatalog.MaxConnections} связей.");
            return;
        }

        _graph.Connections.RemoveAll(connection =>
            connection.ToNode == toNode.Id && connection.ToPort == toPort.Id);

        _graph.Connections.Add(new EffectGraphConnection
        {
            FromNode = fromNodeId,
            FromPort = fromPort,
            ToNode = toNodeId,
            ToPort = toPort.Id,
        });

        StatusMessage?.Invoke("Связь добавлена. Проверьте формулу перед сохранением.");
        Changed();
    }

    private bool HasPath(int fromNodeId, int targetNodeId)
    {
        var pending = new Stack<int>();
        var visited = new HashSet<int>();
        pending.Push(fromNodeId);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current == targetNodeId)
                return true;

            if (!visited.Add(current))
                continue;

            foreach (var connection in _graph.Connections)
            {
                if (connection.FromNode == current)
                    pending.Push(connection.ToNode);
            }
        }

        return false;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        // Draw uses physical pixels, while mouse positions and the graph use virtual pixels.
        // Apply the UI scale around the control origin so both coordinate systems match.
        var oldTransform = handle.GetTransform();
        handle.SetTransform(Matrix3x2.CreateScale(UIScale) * oldTransform);

        handle.DrawRect(SizeBox, Color.FromHex("#101018"));

        // A subtle grid helps players understand that nodes live in a free workspace.
        var gridStep = 32f * _viewScale;
        var gridOffsetX = MathF.IEEERemainder(_viewOffset.X, gridStep);
        var gridOffsetY = MathF.IEEERemainder(_viewOffset.Y, gridStep);
        for (var x = gridOffsetX; x < Size.X; x += gridStep)
            handle.DrawLine(new Vector2(x, 0f), new Vector2(x, Size.Y), Color.FromHex("#191925"));
        for (var y = gridOffsetY; y < Size.Y; y += gridStep)
            handle.DrawLine(new Vector2(0f, y), new Vector2(Size.X, y), Color.FromHex("#191925"));

        foreach (var connection in _graph.Connections)
        {
            var from = _graph.FindNode(connection.FromNode);
            var to = _graph.FindNode(connection.ToNode);
            var fromDefinition = from == null ? null : EffectOperationCatalog.Get(from.Operation);
            var toDefinition = to == null ? null : EffectOperationCatalog.Get(to.Operation);
            var fromPort = fromDefinition?.FindOutput(connection.FromPort);
            var toPort = toDefinition?.FindInput(connection.ToPort);
            if (from == null || to == null || fromPort == null || toPort == null || fromDefinition == null || toDefinition == null)
                continue;

            var fromIndex = Array.IndexOf(fromDefinition.Outputs, fromPort);
            var toIndex = Array.IndexOf(toDefinition.Inputs, toPort);
            if (fromIndex < 0 || toIndex < 0)
                continue;

            var start = OutputPortPosition(from, fromDefinition, fromIndex);
            var end = InputPortPosition(to, toDefinition, toIndex);
            handle.DrawLine(start, end, TypeColor(toPort.Type).WithAlpha(0.7f));
            handle.DrawCircle(end, 3f, TypeColor(toPort.Type));
        }

        if (_connectingFrom is { } pending &&
            _graph.FindNode(pending.Node) is { } pendingNode &&
            EffectOperationCatalog.Get(pendingNode.Operation) is { } pendingDefinition)
        {
            var ports = pending.Output ? pendingDefinition.Outputs : pendingDefinition.Inputs;
            var index = Array.FindIndex(ports, port => port.Id == pending.Port.Id);
            if (index >= 0)
            {
                var start = pending.Output
                    ? OutputPortPosition(pendingNode, pendingDefinition, index)
                    : InputPortPosition(pendingNode, pendingDefinition, index);
                handle.DrawLine(start, _mousePosition,
                    Color.FromHex("#8fb8ff").WithAlpha(0.8f));
            }
        }

        foreach (var node in _graph.Nodes)
            DrawNode(handle, node);

        handle.SetTransform(oldTransform);
    }

    private void DrawNode(DrawingHandleScreen handle, EffectGraphNode node)
    {
        var definition = EffectOperationCatalog.Get(node.Operation);
        if (definition == null)
            return;

        var topLeft = ToScreen(node.Position);
        var height = NodeHeight(definition) * _viewScale;
        var width = NodeWidth * _viewScale;
        var box = new UIBox2(topLeft, topLeft + new Vector2(width, height));
        var selected = node.Id == _selectedNodeId;
        var background = selected ? Color.FromHex("#242a42") : Color.FromHex("#1c1f2e");
        var border = selected ? Color.FromHex("#8fb8ff") : Color.FromHex("#3b4160");

        handle.DrawRect(box, background);
        handle.DrawRect(box, border, false);

        handle.DrawRect(new UIBox2(box.Left, box.Top, box.Right, box.Top + HeaderHeight * _viewScale),
            selected ? Color.FromHex("#344a78") : Color.FromHex("#2a3048"));
        handle.DrawString(_font, topLeft + new Vector2(8f, 7f), definition.Label,
            selected ? Color.White : Color.FromHex("#d8e2ff"));

        for (var i = 0; i < definition.Inputs.Length; i++)
        {
            var port = definition.Inputs[i];
            var position = InputPortPosition(node, definition, i);
            var color = TypeColor(port.Type);
            handle.DrawCircle(position, PortRadius * _viewScale, color);
            handle.DrawString(_smallFont, position + new Vector2(9f, -6f), port.Label, Color.FromHex("#aeb8d8"));
        }

        for (var i = 0; i < definition.Outputs.Length; i++)
        {
            var port = definition.Outputs[i];
            var position = OutputPortPosition(node, definition, i);
            var color = TypeColor(port.Type);
            handle.DrawCircle(position, PortRadius * _viewScale, color);
            var dimensions = handle.GetDimensions(_smallFont, port.Label, 1f);
            handle.DrawString(_smallFont, position - new Vector2(dimensions.X + 9f, 6f), port.Label,
                Color.FromHex("#aeb8d8"));
        }

        if (node.Id == _selectedNodeId)
            handle.DrawRect(new UIBox2(box.Left - 2f, box.Top - 2f, box.Right + 2f, box.Bottom + 2f),
                Color.FromHex("#8fb8ff").WithAlpha(0.35f), false);
    }

    private static Color TypeColor(EffectValueType type)
    {
        return type switch
        {
            EffectValueType.Number => Color.FromHex("#ffce73"),
            EffectValueType.Boolean => Color.FromHex("#7fe09b"),
            EffectValueType.Entity => Color.FromHex("#ff8fb8"),
            EffectValueType.EntityList => Color.FromHex("#d59bff"),
            EffectValueType.Vector => Color.FromHex("#7fd9ff"),
            _ => Color.White,
        };
    }
}
