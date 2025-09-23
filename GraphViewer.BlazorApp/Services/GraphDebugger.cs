using Blazor.Diagrams;
using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Models.Base;
using GraphViewer.BlazorApp.Components.Core;

namespace GraphViewer.BlazorApp.Services;

public sealed class GraphDebugger(GraphConsole GConsole)
{
    public bool Running { get; set; }
    public CancellationTokenSource CancellationSource { get; private set; } = new();
    public AutoResetEvent RunSignal { get; private set; } = new(false);

    public event Action? Rerender;

    public async Task RunAsync(BlazorDiagram diagram, bool reverse_order)
    {
        GConsole.Clear();
        await StopAsync(diagram);
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
        await Task.Run(RunSync, CancellationSource.Token);

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
            Stack<GraphNode.Model> visited_nodes = [];
            Stack<IEnumerable<BaseLinkModel>> links_to_visit = [];
            visited_nodes.Push(source);
            source.State |= GraphNode.State.Current;
            source.State |= GraphNode.State.PathPart;
            source.Refresh();
            links_to_visit.Push(reverse_order is true ? source.Links.Reverse() : source.Links);
            GConsole.WriteLine($"Start with N[{source.Order}]");
            while(links_to_visit.Count > 0)
            {
                bool new_node = false;
                foreach(BaseLinkModel model in links_to_visit.Peek())
                {
                    bool break_this = false;
                    var link = (LinkModel)model;
                    var node = visited_nodes.Peek();
                    if(link.Source.Model == node)
                    {
                        link.TargetMarker = LinkMarker.Arrow;
                        link.SourceMarker = LinkMarker.Circle;
                    }
                    else
                    {
                        link.SourceMarker = LinkMarker.Arrow;
                        link.TargetMarker = LinkMarker.Circle;
                    }
                    link.Color ??= "gray";
                    link.Refresh();
                    RunSignal.WaitOne();
                    CancellationSource.Token.ThrowIfCancellationRequested();
                    if(link.Color is "gray")
                    {
                        link.Color = null;
                        link.Refresh();
                        Rerender?.Invoke();
                    }
                    if(link.Color is not null)
                    {
                        goto NextLink;
                    }
                    var next_node = (GraphNode.Model?)(link.Source.Model!.Equals(visited_nodes.Peek()) is true ? link.Target.Model : link.Source.Model);
                    if(next_node is null || visited_nodes.Contains(next_node) is true)
                    {
                        goto NextLink;
                    }
                    break_this = true;
                    new_node = true;
                    node.State &= ~GraphNode.State.Current;
                    node.Refresh();
                    visited_nodes.Push(next_node);
                    next_node.State |= GraphNode.State.Current;
                    next_node.State |= GraphNode.State.PathPart;
                    next_node.Refresh();
                    link.Color = "indianred";
                    link.Refresh();
                    GConsole.WriteLine($"N[{node.Order}]->N[{next_node.Order}]");
                    if(next_node == destination)
                    {
                        link.SourceMarker = null;
                        link.TargetMarker = null;
                        link.Refresh();
                        next_node.State &= ~GraphNode.State.Current;
                        next_node.Refresh();
                        GConsole.WriteLine("Destination node reached");
                        GConsole.WriteLine($"Path: {string.Join("->", visited_nodes.Reverse().Select(node => $"N[{node.Order}]"))}");
                        Running = false;
                        return;
                    }
                    links_to_visit.Push(reverse_order is true ? next_node.Links.Reverse() : next_node.Links);
                    NextLink: 
                    {
                        link.SourceMarker = null;
                        link.TargetMarker = null;
                        link.Refresh();
                        Rerender?.Invoke();
                        if(break_this is true)
                        {
                            break;
                        }
                    }
                }
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
                    GConsole.WriteLine("Path not found");
                    Running = false;
                    return;
                }
                previous_node.State |= GraphNode.State.Current;
                previous_node.Refresh();
                links_to_visit.Pop();
                BaseLinkModel[] links = links_to_visit.Pop().SkipWhile(link => link.Source.Model != this_node && link.Target.Model != this_node).ToArray();
                (links[0] as LinkModel)?.Color = null;
                links[0].Refresh();
                links_to_visit.Push(links.Skip(1).ToArray());
                GConsole.WriteLine($"N[{previous_node.Order}]<-N[{this_node.Order}]");
            }
        }
    }
    public async ValueTask StopAsync(BlazorDiagram diagram)
    {
        diagram.Nodes.OfType<GraphNode.Model>().ToList().ForEach(node =>
        {
            node.State &= ~GraphNode.State.Current;
            node.State &= ~GraphNode.State.PathPart;
            node.Refresh();
        });
        diagram.Links.OfType<LinkModel>().ToList().ForEach(link =>
        {
            link.Color = null;
            link.SourceMarker = null;
            link.TargetMarker = null;
            link.Refresh();
        });
        Task cancel_task = CancellationSource.CancelAsync();
        RunSignal.Set();
        await cancel_task;
        CancellationSource.Dispose();
        RunSignal.Dispose();
        CancellationSource = new CancellationTokenSource();
        RunSignal = new AutoResetEvent(false);
        Running = false;
        Rerender?.Invoke();
    }
}