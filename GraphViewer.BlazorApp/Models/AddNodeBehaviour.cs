using Blazor.Diagrams.Core;
using Blazor.Diagrams.Core.Events;
using Blazor.Diagrams.Core.Models.Base;
using GraphViewer.BlazorApp.Components.Core;

namespace GraphViewer.BlazorApp.Models;

public sealed partial class AddNodeBehavior : Blazor.Diagrams.Core.Behavior
{
    public AddNodeBehavior(Diagram diagram) : base(diagram)
    {
        Diagram.PointerDoubleClick += OnDoubleClick;
    }

    private void OnDoubleClick(Model? model, MouseEventArgs event_args)
    {
        if(model is not null)
        {
            return;
        }
        GraphNode.Model node = new(Diagram.GetRelativeMousePoint(event_args.ClientX - 25, event_args.ClientY - 25))
        {
            Title = "",
            Order = Diagram.Nodes.Count + 1,
        };
        Diagram.Nodes.Add(node);
    }

    public override void Dispose()
    {
        Diagram.PointerDoubleClick -= OnDoubleClick;
    }
}