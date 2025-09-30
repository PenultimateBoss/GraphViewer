using Blazor.Diagrams;
using System.Diagnostics;
using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Models.Base;
using GraphViewer.BlazorApp.Components.Core;

namespace GraphViewer.BlazorApp.Services;

public sealed class GraphDebugger(GraphConsole GConsole)
{
    public bool Running { get; set; } = false;
    public int StepTime { get; set; } = 1;
    public int ActionCount { get; private set; } = 0;
    public HashSet<GraphNode.Model> OpenedNodes { get; } = [];

    public event Action? Rerender;

    public async Task RunAsync(BlazorDiagram diagram, bool reverse_order)
    {
        Stop(diagram);
        if(Running is true)
        {
            return;
        }
        if(FindSource(diagram) is not GraphNode.Model source)
        {
            GConsole.WriteLine("No source node found");
            return;
        }
        if(FindDestination(diagram) is not GraphNode.Model destination)
        {
            GConsole.WriteLine("No destination node found");
            return;
        }
        if(source == destination)
        {
            GConsole.WriteLine("Source and destination nodes are the same");
            return;
        }
        if(source.Links.Count is 0)
        {
            GConsole.WriteLine("Source node has no links");
            return;
        }
        if(destination.Links.Count is 0)
        {
            GConsole.WriteLine("Destination node has no links");
            return;
        }
        Running = true;
        await Task.Run(RunSync);

        static GraphNode.Model? FindSource(BlazorDiagram diagram)
        {
            return (GraphNode.Model?)diagram.Nodes.FirstOrDefault(model => model is GraphNode.Model node && node.State is GraphNode.State.Source);
        }
        static GraphNode.Model? FindDestination(BlazorDiagram diagram)
        {
            return (GraphNode.Model?)diagram.Nodes.FirstOrDefault(model => model is GraphNode.Model node && node.State is GraphNode.State.Destination);
        }
        void RunSync()
        {
            TimeSpan time = TimeSpan.Zero;
            long timestamp = Stopwatch.GetTimestamp();
            Stack<GraphNode.Model> visited_nodes = [];
            Stack<IEnumerable<BaseLinkModel>> links_to_visit = [];
            visited_nodes.Push(source);
            source.State |= GraphNode.State.Current;
            source.State |= GraphNode.State.PathPart;
            source.Refresh();
            OpenedNodes.Add(source);
            links_to_visit.Push(reverse_order is true ? source.Links.Reverse() : source.Links);
            while(links_to_visit.Count > 0)
            {
                bool new_node = false;
                foreach(BaseLinkModel model in links_to_visit.Peek())
                {
                    bool break_this = false;
                    var link = (LinkModel)model;
                    var node = visited_nodes.Peek();
                    if(link.Source.Model == node && link.SourceMarker is not null)
                    {
                        continue;
                    }
                    if(link.Target.Model == node && link.TargetMarker is not null)
                    {
                        continue;
                    }
                    var prev_color = link.Color;
                    link.Color = "yellow";
                    link.Refresh();
                    Rerender?.Invoke();
                    time += Stopwatch.GetElapsedTime(timestamp);
                    Task.Delay(StepTime * 500).GetAwaiter().GetResult();
                    timestamp = Stopwatch.GetTimestamp();
                    link.Color = prev_color;
                    link.Refresh();
                    Rerender?.Invoke();
                    if(link.Color is not null)
                    {
                        goto NextLink;
                    }
                    var next_node = (GraphNode.Model?)(link.Source.Model!.Equals(visited_nodes.Peek()) is true ? link.Target.Model : link.Source.Model);
                    if(next_node is null || visited_nodes.Contains(next_node) is true)
                    {
                        goto NextLink;
                    }
                    ActionCount += 1;
                    OpenedNodes.Add(next_node);
                    break_this = true;
                    new_node = true;
                    node.State &= ~GraphNode.State.Current;
                    visited_nodes.Push(next_node);
                    next_node.State |= GraphNode.State.Current;
                    next_node.State |= GraphNode.State.PathPart;
                    link.Color = "indianred";
                    node.Refresh();
                    next_node.Refresh();
                    link.Refresh();
                    if(next_node == destination)
                    {
                        next_node.State &= ~GraphNode.State.Current;
                        next_node.Refresh();
                        GConsole.WriteLine("Destination node reached!");
                        GConsole.WriteLine($"NodeCount: {visited_nodes.Count}");
                        GConsole.WriteLine($"EdgeCount: {diagram.Links.Where(link => link is LinkModel model && model.Color is "indianred").Count()}");
                        GConsole.WriteLine($"ActionCount: {ActionCount}");
                        GConsole.WriteLine($"OpenedNodeCount: {OpenedNodes.Count}");
                        GConsole.WriteLine($"Time: {time + Stopwatch.GetElapsedTime(timestamp)}");
                        GConsole.WriteLine($"Path: {string.Join(" => ", visited_nodes.Reverse().Select(node => $"N[{node.Order}]"))}");
                        return;
                    }
                    links_to_visit.Push(reverse_order is true ? next_node.Links.Reverse() : next_node.Links);
                    NextLink: 
                    {
                        Rerender?.Invoke();
                        if(break_this is true)
                        {
                            break;
                        }
                    }
                }
                ActionCount += 1;
                if(new_node is true)
                {
                    continue;
                }
                var this_node = visited_nodes.Peek();
                this_node.State &= ~GraphNode.State.Current;
                this_node.State &= ~GraphNode.State.PathPart;
                this_node.Refresh();
                visited_nodes.Pop();
                if(visited_nodes.TryPeek(out GraphNode.Model? previous_node) is false)
                {
                    GConsole.WriteLine("Path not found!");
                    return;
                }
                previous_node.State |= GraphNode.State.Current;
                previous_node.Refresh();
                links_to_visit.Pop();
                BaseLinkModel[] links = links_to_visit.Pop().SkipWhile(link => link.Source.Model != this_node && link.Target.Model != this_node).ToArray();
                (links[0] as LinkModel)?.Color = null;
                links[0].Refresh();
                links_to_visit.Push(links.Skip(1).ToArray());
                Rerender?.Invoke();
                time += Stopwatch.GetElapsedTime(timestamp);
                Task.Delay(StepTime * 500).GetAwaiter().GetResult();
                timestamp = Stopwatch.GetTimestamp();
            }
        }
    }
    public void Stop(BlazorDiagram diagram)
    {
        ActionCount = 0;
        OpenedNodes.Clear();
        diagram.Nodes.OfType<GraphNode.Model>().ToList().ForEach(node =>
        {
            node.State &= ~GraphNode.State.Current;
            node.State &= ~GraphNode.State.PathPart;
            node.Refresh();
        });
        diagram.Links.OfType<LinkModel>().ToList().ForEach(link =>
        {
            link.Color = null;
            link.Refresh();
        });
        GConsole.Clear();
        Running = false;
    }
}