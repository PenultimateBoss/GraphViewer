using SvgPathProperties;
using Blazor.Diagrams.Core;
using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Models.Base;
using Blazor.Diagrams.Core.PathGenerators;

namespace GraphViewer.BlazorApp.Models;

public sealed class LoopPathGenerator : PathGenerator
{
    public override PathGeneratorResult GetResult(Diagram diagram, BaseLinkModel link, Blazor.Diagrams.Core.Geometry.Point[] route, Blazor.Diagrams.Core.Geometry.Point source, Blazor.Diagrams.Core.Geometry.Point target)
    {
        if(link.Source.Model is not NodeModel)
        {
            return new PathGeneratorResult(new SvgPath(), []);
        }
        Blazor.Diagrams.Core.Geometry.Point center = link.Source.GetPlainPosition()!;
        SvgPath path = new();
        path.AddMoveTo(center.X, center.Y);
        path.AddCubicBezierCurve(center.X + 50, center.Y + 50, center.X - 50, center.Y + 50, center.X, center.Y);
        path.AddClosePath();
        return new PathGeneratorResult(path, []);
    }
}