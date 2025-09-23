using Blazor.Diagrams.Core;
using Blazor.Diagrams.Core.Events;
using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Anchors;
using Blazor.Diagrams.Core.Models.Base;
using Blazor.Diagrams.Core.PathGenerators;

namespace GraphViewer.BlazorApp.Models;

public sealed partial class GraphDragBehavior : Blazor.Diagrams.Core.Behavior
{
    private bool Moved { get; set; }
    private double? LastClientX { get; set; }
    private double? LastClientY { get; set; }
    private PositionAnchor? TargetPositionAnchor { get; set; }
    private Dictionary<MovableModel, Blazor.Diagrams.Core.Geometry.Point> InitialPositions { get; set; } = [];
    public BaseLinkModel? OngoingLink { get; private set; }

    public GraphDragBehavior(Diagram diagram) : base(diagram)
    {
        Diagram.PointerDown += OnPointerDown;
        Diagram.PointerMove += OnPointerMove;
        Diagram.PointerUp += OnPointerUp;
    }

    private void OnPointerDown(Model? model, MouseEventArgs event_args)
    {
        OngoingLink = null;
        TargetPositionAnchor = null;
        if(model is NodeModel node && event_args.Button == (long)MouseEventButton.Right)
        {
            if(node.Locked)
            {
                return;
            }
            TargetPositionAnchor = new PositionAnchor(CalculateTargetPosition(event_args.ClientX, event_args.ClientY));
            OngoingLink = Diagram.Options.Links.Factory(Diagram, node, TargetPositionAnchor);
            if(OngoingLink == null)
            {
                return;
            }
            OngoingLink.SetTarget(TargetPositionAnchor);
            OngoingLink.PathGenerator = new StraightPathGenerator();
            Diagram.Links.Add(OngoingLink);
        }
        else if(model is MovableModel && event_args.Button == (long)MouseEventButton.Left)
        {
            InitialPositions.Clear();
            foreach(SelectableModel sm in Diagram.GetSelectedModels())
            {
                if(sm is not MovableModel movable || movable.Locked)
                {
                    continue;
                }
                if(sm is NodeModel n && n.Group != null && !n.Group.AutoSize)
                {
                    continue;
                }
                Blazor.Diagrams.Core.Geometry.Point position = movable.Position;
                if(Diagram.Options.GridSnapToCenter && movable is NodeModel n2)
                {
                    position = new Blazor.Diagrams.Core.Geometry.Point(movable.Position.X + (n2.Size?.Width ?? 0) / 2, movable.Position.Y + (n2.Size?.Height ?? 0) / 2);
                }
                InitialPositions.Add(movable, position);
            }
            LastClientX = event_args.ClientX;
            LastClientY = event_args.ClientY;
            Moved = false;
        }
    }
    private void OnPointerMove(Model? model, MouseEventArgs event_args)
    {
        if(OngoingLink is not null && model is null)
        {
            TargetPositionAnchor!.SetPosition(CalculateTargetPosition(event_args.ClientX, event_args.ClientY));
            if(Diagram.Options.Links.EnableSnapping is true)
            {
                NodeModel? near_node = FindNearNodeToAttachTo();
                if(near_node is not null || OngoingLink.Target is not PositionAnchor)
                {
                    OngoingLink.SetTarget(near_node is null ? TargetPositionAnchor : new ShapeIntersectionAnchor(near_node));
                }
            }
            OngoingLink.Refresh();
            OngoingLink.RefreshLinks();
        }
        else if(InitialPositions.Count is not 0 && LastClientX is not null && LastClientY is not null)
        {
            Moved = true;
            double delta_x = (event_args.ClientX - LastClientX.Value) / Diagram.Zoom;
            double delta_y = (event_args.ClientY - LastClientY.Value) / Diagram.Zoom;
            foreach((MovableModel movable, Blazor.Diagrams.Core.Geometry.Point initial_position) in InitialPositions)
            {
                var ndx = ApplyGridSize(delta_x + initial_position.X);
                var ndy = ApplyGridSize(delta_y + initial_position.Y);
                if(Diagram.Options.GridSnapToCenter && movable is NodeModel node)
                {
                    node.SetPosition(ndx - (node.Size?.Width ?? 0) / 2, ndy - (node.Size?.Height ?? 0) / 2);
                }
                else
                {
                    movable.SetPosition(ndx, ndy);
                }
            }
        }    
    }
    private void OnPointerUp(Model? model, MouseEventArgs event_args)
    {
        if(OngoingLink is not null)
        {
            if(OngoingLink.IsAttached is true) // Snapped already
            {
                OngoingLink.TriggerTargetAttached();
                OngoingLink = null;
                return;
            }
            if(model is ILinkable linkable && (OngoingLink.Source.Model is null || OngoingLink.Source.Model.CanAttachTo(linkable) is true))
            {
                var targetAnchor = Diagram.Options.Links.TargetAnchorFactory(Diagram, OngoingLink, linkable);
                OngoingLink.SetTarget(targetAnchor);
                OngoingLink.TriggerTargetAttached();
                OngoingLink.Refresh();
                OngoingLink.RefreshLinks();
            }
            else if(Diagram.Options.Links.RequireTarget is true)
            {
                Diagram.Links.Remove(OngoingLink);
            }
            else if(Diagram.Options.Links.RequireTarget is false)
            {
                OngoingLink.Refresh();
            }
            OngoingLink = null;
        }
        else if(InitialPositions.Count is not 0)
        {
            if(Moved is true)
            {
                foreach((MovableModel movable, _) in InitialPositions)
                {
                    movable.TriggerMoved();
                }
            }
            InitialPositions.Clear();
            LastClientX = null;
            LastClientY = null;
        }
    }
    private NodeModel? FindNearNodeToAttachTo()
    {
        if(OngoingLink is null || TargetPositionAnchor is null)
        {
            return null;
        }
        NodeModel? nearest_node = null;
        double nearest_distance = double.PositiveInfinity;
        Blazor.Diagrams.Core.Geometry.Point position = TargetPositionAnchor!.GetPosition(OngoingLink)!;
        foreach(NodeModel node in Diagram.Nodes)
        {
            double distance = position.DistanceTo(node.Position);
            if(distance <= Diagram.Options.Links.SnappingRadius && OngoingLink.Source.Model?.CanAttachTo(node) is true or null)
            {
                if(distance < nearest_distance)
                {
                    nearest_distance = distance;
                    nearest_node = node;
                }
            }
        }
        return nearest_node;
    }
    private double ApplyGridSize(double n)
    {
        if(Diagram.Options.GridSize == null)
            return n;

        var gridSize = Diagram.Options.GridSize.Value;
        return gridSize * Math.Floor((n + gridSize / 2.0) / gridSize);
    }
    private Blazor.Diagrams.Core.Geometry.Point CalculateTargetPosition(double client_x, double client_y)
    {
        Blazor.Diagrams.Core.Geometry.Point target = Diagram.GetRelativeMousePoint(client_x, client_y);
        if(OngoingLink is null)
        {
            return target;
        }
        Blazor.Diagrams.Core.Geometry.Point source = OngoingLink.Source.GetPlainPosition()!;
        Blazor.Diagrams.Core.Geometry.Point dir_vector = target.Subtract(source).Normalize();
        Blazor.Diagrams.Core.Geometry.Point change = dir_vector.Multiply(5);
        return target.Subtract(change);
    }
    public void StartFrom(ILinkable source, double client_x, double client_y)
    {
        if(OngoingLink is not null)
        {
            return;
        }
        TargetPositionAnchor = new PositionAnchor(CalculateTargetPosition(client_x, client_y));
        OngoingLink = Diagram.Options.Links.Factory(Diagram, source, TargetPositionAnchor);
        if(OngoingLink is null)
        {
            return;
        }
        Diagram.Links.Add(OngoingLink);
    }
    public void StartFrom(BaseLinkModel link, double client_x, double client_y)
    {
        if(OngoingLink is not null)
        {
            return;
        }
        TargetPositionAnchor = new PositionAnchor(CalculateTargetPosition(client_x, client_y));
        OngoingLink = link;
        OngoingLink.SetTarget(TargetPositionAnchor);
        OngoingLink.Refresh();
        OngoingLink.RefreshLinks();
    }

    public override void Dispose()
    {
        InitialPositions.Clear();
        Diagram.PointerDown -= OnPointerDown;
        Diagram.PointerMove -= OnPointerMove;
        Diagram.PointerUp -= OnPointerUp;
    }
}