using Koreny.Models;

namespace Koreny.Services;

/// <summary>
/// Čistý (bez UI) výpočet layoutu potomkovského stromu — extrahováno z DescendantTree.razor
/// kvůli testovatelnosti (viz docs/audit-renderer.md, sekce 3.3, kolize párů).
///
/// Slot = jednotka vodorovného rastru o rozteči <see cref="SlotPitch"/>. Listy dostávají sloty
/// post-orderem; pár (uzel s partnerem) se kreslí jako dvojbox široký <see cref="PairWidth"/>,
/// takže musí zabírat rozsah aspoň 2 slotů — jinak by se sousední páry překrývaly.
/// </summary>
public static class DescendantLayout
{
    public const double NodeWidth = 160;
    public const double NodeHeight = 56;
    public const double HGap = 24;
    public const double VGap = 80;
    public const double Padding = 24;

    public static double SlotPitch => NodeWidth + HGap;
    public static double RowPitch => NodeHeight + VGap;

    /// <summary>Šířka vykresleného páru (dvojbox: box + mezera + box) = 344 px.</summary>
    public static double PairWidth => NodeWidth + HGap + NodeWidth;

    public static double RangeCenterX(int slotMin, int slotMax)
    {
        var left = Padding + slotMin * SlotPitch;
        var right = Padding + slotMax * SlotPitch + NodeWidth;
        return (left + right) * 0.5;
    }

    /// <summary>Střed vykresleného páru (nebo jednoho boxu) pro daný uzel.</summary>
    public static double CoupleCenterX(DescNode n) => RangeCenterX(n.SlotMin, n.SlotMax);

    /// <summary>Postaví strom potomků a přiřadí slot-rozsahy (vč. rozšíření párů na ≥ 2 sloty).</summary>
    public static DescNode? BuildLaidOutTree(GedcomDocument document, string rootId, out int slotCount)
    {
        slotCount = 0;
        var root = BuildDescNode(document, rootId, new HashSet<string>(StringComparer.Ordinal));
        if (root is null)
        {
            return null;
        }

        var next = 0;
        AssignLeafSlots(ref next, root);
        slotCount = next;
        return root;
    }

    public static DescNode? BuildLaidOutTree(GedcomDocument document, string rootId) =>
        BuildLaidOutTree(document, rootId, out _);

    public static DescendantLayoutResult? Compute(GedcomDocument document, string rootId)
    {
        var root = BuildLaidOutTree(document, rootId, out var slotCount);
        if (root is null)
        {
            return null;
        }

        var segments = new List<DescSegment>();
        var boxes = new List<DescBox>();
        LayoutGeometry(root, 0, segments, boxes);

        var width = slotCount <= 0
            ? Padding * 2 + NodeWidth
            : Padding * 2 + (slotCount - 1) * SlotPitch + NodeWidth;
        var generations = GetMaxDepth(root) + 1;
        var height = generations * (NodeHeight + VGap);

        return new DescendantLayoutResult
        {
            Boxes = boxes,
            Segments = segments,
            Width = width,
            Height = height,
        };
    }

    private static int GetMaxDepth(DescNode n)
    {
        if (n.Children.Count == 0)
        {
            return 0;
        }

        return 1 + n.Children.Max(GetMaxDepth);
    }

    private static DescNode? BuildDescNode(GedcomDocument document, string personId, HashSet<string> visiting)
    {
        if (!visiting.Add(personId))
        {
            return new DescNode { LeafId = personId };
        }

        try
        {
            var fams = document.Families
                .Where(f => f.HusbandId == personId || f.WifeId == personId)
                .ToList();
            if (fams.Count == 0)
            {
                return new DescNode { LeafId = personId };
            }

            var fam = fams[0];
            var childIds = fams.SelectMany(f => f.ChildrenIds).Distinct(StringComparer.Ordinal).ToList();
            var children = new List<DescNode>();
            foreach (var cid in childIds)
            {
                var ch = BuildDescNode(document, cid, visiting);
                if (ch is not null)
                {
                    children.Add(ch);
                }
            }

            return new DescNode
            {
                HusbId = fam.HusbandId,
                WifeId = fam.WifeId,
                Children = children,
            };
        }
        finally
        {
            visiting.Remove(personId);
        }
    }

    /// <summary>
    /// Post-order přiřazení slotů. Pár potřebuje rozsah aspoň 2 slotů, aby se jeho dvojbox
    /// (344 px) vešel; když je podstrom užší (např. jediné dítě-list), rezervuje se další slot,
    /// takže se sousední páry nepřekryjí. Bezdětný pár rezervuje 2 sloty rovnou.
    /// </summary>
    private static void AssignLeafSlots(ref int nextSlot, DescNode n)
    {
        var isPair = !string.IsNullOrEmpty(n.HusbId) && !string.IsNullOrEmpty(n.WifeId);

        if (n.Children.Count > 0)
        {
            foreach (var c in n.Children)
            {
                AssignLeafSlots(ref nextSlot, c);
            }

            n.SlotMin = n.Children.Min(c => c.SlotMin);
            n.SlotMax = n.Children.Max(c => c.SlotMax);

            if (isPair && n.SlotMax - n.SlotMin < 1)
            {
                n.SlotMax = n.SlotMin + 1;
                if (nextSlot <= n.SlotMax)
                {
                    nextSlot = n.SlotMax + 1; // rezervuj slot, aby ho nedostal další sourozenec
                }
            }

            return;
        }

        if (n.LeafId is not null)
        {
            n.SlotMin = n.SlotMax = nextSlot++;
            return;
        }

        if (isPair)
        {
            n.SlotMin = nextSlot;
            n.SlotMax = nextSlot + 1;
            nextSlot += 2;
            return;
        }

        n.SlotMin = n.SlotMax = nextSlot++;
    }

    /// <summary>Trunk drops from couple midline; bendY splits between generations so edges do not cross boxes.</summary>
    private static void LayoutGeometry(DescNode n, int depth, List<DescSegment> segments, List<DescBox> boxes)
    {
        var cx = RangeCenterX(n.SlotMin, n.SlotMax);
        var y = depth * RowPitch;
        var rowMidY = y + NodeHeight * 0.5;

        if (n.IsLeaf)
        {
            boxes.Add(new DescBox(n.LeafId!, cx, y));
            return;
        }

        var h = n.HusbId;
        var w = n.WifeId;
        if (!string.IsNullOrEmpty(h) && !string.IsNullOrEmpty(w))
        {
            var totalW = NodeWidth + HGap + NodeWidth;
            var leftCx = cx - totalW * 0.5 + NodeWidth * 0.5;
            var rightCx = cx + totalW * 0.5 - NodeWidth * 0.5;
            boxes.Add(new DescBox(h, leftCx, y));
            boxes.Add(new DescBox(w, rightCx, y));
            segments.Add(new DescSegment(leftCx + NodeWidth * 0.5, rowMidY, rightCx - NodeWidth * 0.5, rowMidY));
        }
        else
        {
            var only = !string.IsNullOrEmpty(h) ? h : w;
            if (!string.IsNullOrEmpty(only))
            {
                boxes.Add(new DescBox(only, cx, y));
            }
        }

        if (n.Children.Count == 0)
        {
            return;
        }

        var childTopY = (depth + 1) * RowPitch;
        var bendY = rowMidY + (childTopY - rowMidY) * 0.5;

        segments.Add(new DescSegment(cx, rowMidY, cx, bendY));

        if (n.Children.Count == 1)
        {
            var ccx = RangeCenterX(n.Children[0].SlotMin, n.Children[0].SlotMax);
            if (Math.Abs(ccx - cx) > 0.01)
            {
                segments.Add(new DescSegment(cx, bendY, ccx, bendY));
            }

            segments.Add(new DescSegment(ccx, bendY, ccx, childTopY));
        }
        else
        {
            var xs = n.Children.Select(c => RangeCenterX(c.SlotMin, c.SlotMax)).ToList();
            segments.Add(new DescSegment(xs.Min(), bendY, xs.Max(), bendY));
            foreach (var x in xs)
            {
                segments.Add(new DescSegment(x, bendY, x, childTopY));
            }
        }

        foreach (var c in n.Children)
        {
            LayoutGeometry(c, depth + 1, segments, boxes);
        }
    }
}

public sealed class DescNode
{
    public string? LeafId { get; init; }
    public string? HusbId { get; init; }
    public string? WifeId { get; init; }
    public List<DescNode> Children { get; init; } = new();
    public int SlotMin { get; set; }
    public int SlotMax { get; set; }

    public bool IsLeaf => Children.Count == 0 && LeafId is not null;
}

public readonly record struct DescBox(string Id, double CenterX, double Y);

public readonly record struct DescSegment(double X1, double Y1, double X2, double Y2);

public sealed class DescendantLayoutResult
{
    public required List<DescBox> Boxes { get; init; }
    public required List<DescSegment> Segments { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
}
