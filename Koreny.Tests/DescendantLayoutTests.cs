using Koreny.Models;
using Koreny.Services;

namespace Koreny.Tests;

/// <summary>
/// Výpočtové testy layoutu potomkovského stromu (docs/audit-renderer.md, 3.3 — kolize párů).
/// Testují čistou vrstvu <see cref="DescendantLayout"/>, žádné SVG ani UI.
/// </summary>
public class DescendantLayoutTests
{
    private static GedcomIndividual Ind(string id, string sex) => new() { Id = id, Sex = sex };

    /// <summary>
    /// Dva sousední páry, každý s jediným dítětem-listem. Před opravou dostal každý pár rozsah
    /// šířky 1 slotu (středy 184 px od sebe) a dvojboxy (344 px) se překrývaly. Po opravě má
    /// každý pár rozsah ≥ 2 slotů, takže vzdálenost středů ≥ šířka dvojboxu.
    /// </summary>
    [Fact]
    public void AdjacentPairs_DoNotOverlap()
    {
        var doc = new GedcomDocument();
        doc.Individuals.AddRange(new[]
        {
            Ind("I1", "M"), Ind("I2", "F"), // kořenový pár
            Ind("I3", "M"), Ind("I5", "F"), // levý pár
            Ind("I4", "M"), Ind("I6", "F"), // pravý pár
            Ind("I7", "M"), Ind("I8", "F"), // listy (děti párů)
        });
        doc.Families.AddRange(new[]
        {
            new GedcomFamily { Id = "F1", HusbandId = "I1", WifeId = "I2", ChildrenIds = { "I3", "I4" } },
            new GedcomFamily { Id = "F3", HusbandId = "I3", WifeId = "I5", ChildrenIds = { "I7" } },
            new GedcomFamily { Id = "F4", HusbandId = "I4", WifeId = "I6", ChildrenIds = { "I8" } },
        });

        var root = DescendantLayout.BuildLaidOutTree(doc, "I1");
        Assert.NotNull(root);
        Assert.Equal(2, root!.Children.Count);

        var left = root.Children[0];
        var right = root.Children[1];

        // Každý pár s jedním dítětem musí zabírat rozsah aspoň 2 slotů (jádro opravy).
        Assert.True(left.SlotMax - left.SlotMin >= 1, $"levý pár má rozsah {left.SlotMin}..{left.SlotMax}");
        Assert.True(right.SlotMax - right.SlotMin >= 1, $"pravý pár má rozsah {right.SlotMin}..{right.SlotMax}");

        var distance = Math.Abs(DescendantLayout.CoupleCenterX(right) - DescendantLayout.CoupleCenterX(left));

        // Vzdálenost středů ≥ šířka dvojboxu (344) + minimální mezera (H_GAP = 24) → žádný překryv.
        Assert.True(
            distance >= DescendantLayout.PairWidth + DescendantLayout.HGap,
            $"středy sousedních párů jsou {distance:F0} px od sebe, což je méně než dvojbox {DescendantLayout.PairWidth} + mezera {DescendantLayout.HGap}");
    }

    /// <summary>Bezdětný pár musí rovněž zabírat 2 sloty (dvojbox se vejde).</summary>
    [Fact]
    public void ChildlessPair_ReservesTwoSlots()
    {
        var doc = new GedcomDocument();
        doc.Individuals.AddRange(new[] { Ind("I1", "M"), Ind("I2", "F") });
        doc.Families.Add(new GedcomFamily { Id = "F1", HusbandId = "I1", WifeId = "I2" });

        var root = DescendantLayout.BuildLaidOutTree(doc, "I1");

        Assert.NotNull(root);
        Assert.True(root!.SlotMax - root.SlotMin >= 1);
    }
}
