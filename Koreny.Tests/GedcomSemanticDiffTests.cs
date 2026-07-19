namespace Koreny.Tests;

/// <summary>
/// Testy diffu samotného — diff je měřicí přístroj round-trip testů,
/// takže musí být ověřen nezávisle na parseru/writeru aplikace.
/// </summary>
public class GedcomSemanticDiffTests
{
    [Fact]
    public void Compare_SemanticallyIdenticalTexts_EmptyReport()
    {
        // Levá strana: CRLF, trailing mezery, poznámka rozlámaná CONC/CONT,
        // hlavička s razítkem zdrojové aplikace (SOUR/VERS/DATE/TIME/FILE).
        var expected =
            "0 HEAD\r\n" +
            "1 SOUR MYHERITAGE\r\n" +
            "2 VERS 6.0\r\n" +
            "1 DATE 1 JAN 2020\r\n" +
            "1 GEDC\r\n" +
            "2 VERS 5.5.1\r\n" +
            "2 FORM Lineage-Linked\r\n" +
            "1 CHAR UTF-8\r\n" +
            "0 @I1@ INDI\r\n" +
            "1 NAME Jan /Novák/   \r\n" +
            "1 NOTE První část poznámky,\r\n" +
            "2 CONC  která pokračuje na témže logickém řádku\r\n" +
            "2 CONT a tady na novém.\r\n" +
            "0 TRLR\r\n";

        // Pravá strana: LF, jiné razítko v HEAD, poznámka jako jediný fyzický řádek
        // s CONT — logická hodnota je stejná.
        var actual =
            "0 HEAD\n" +
            "1 SOUR Koreny\n" +
            "1 GEDC\n" +
            "2 VERS 5.5.1\n" +
            "2 FORM Lineage-Linked\n" +
            "1 CHAR UTF-8\n" +
            "0 @I1@ INDI\n" +
            "1 NAME Jan /Novák/\n" +
            "1 NOTE První část poznámky, která pokračuje na témže logickém řádku\n" +
            "2 CONT a tady na novém.\n" +
            "0 TRLR\n";

        var report = GedcomSemanticDiff.Compare(expected, actual);

        Assert.True(report.IsEmpty, report.ToString());
    }

    [Fact]
    public void Compare_KnownDifferences_ReportsAllFourKinds()
    {
        var expected =
            "0 @I1@ INDI\n" +
            "1 NAME Jan /Novák/\n" +
            "1 SEX M\n" +
            "1 BIRT\n" +
            "2 DATE 1900\n" +
            "2 PLAC Praha\n" +
            "1 _MHID 42\n" +
            "0 @I2@ INDI\n" +
            "1 NAME Marie /Nováková/\n" +
            "0 @F1@ FAM\n" +
            "1 HUSB @I1@\n" +
            "0 TRLR\n";

        // Rozdíly: BIRT>DATE jiná hodnota; _MHID chybí; SEX přebývá u @I2@;
        // pořadí záznamů @I2@/@F1@ prohozené.
        var actual =
            "0 @I1@ INDI\n" +
            "1 NAME Jan /Novák/\n" +
            "1 SEX M\n" +
            "1 BIRT\n" +
            "2 DATE 1901\n" +
            "2 PLAC Praha\n" +
            "0 @F1@ FAM\n" +
            "1 HUSB @I1@\n" +
            "0 @I2@ INDI\n" +
            "1 NAME Marie /Nováková/\n" +
            "1 SEX F\n" +
            "0 TRLR\n";

        var report = GedcomSemanticDiff.Compare(expected, actual);

        Assert.Contains(report.Entries, e =>
            e.Kind == GedcomDiffKind.ValueMismatch
            && e.Path == "@I1@ INDI > BIRT > DATE"
            && e.Detail.Contains("'1900'")
            && e.Detail.Contains("'1901'"));

        Assert.Contains(report.Entries, e =>
            e.Kind == GedcomDiffKind.MissingNode
            && e.Path == "@I1@ INDI > _MHID");

        Assert.Contains(report.Entries, e =>
            e.Kind == GedcomDiffKind.ExtraNode
            && e.Path == "@I2@ INDI > SEX");

        Assert.Contains(report.Entries, e =>
            e.Kind == GedcomDiffKind.OrderMismatch
            && e.Path == "(záznamy levelu 0)");

        Assert.Equal(4, report.Entries.Count);
    }

    [Fact]
    public void Compare_ConcMidWord_DoesNotInsertSpace()
    {
        var expected =
            "0 @I1@ INDI\n" +
            "1 NOTE rozdě\n" +
            "2 CONC lené\n" +
            "0 TRLR\n";

        var actual =
            "0 @I1@ INDI\n" +
            "1 NOTE rozdělené\n" +
            "0 TRLR\n";

        var report = GedcomSemanticDiff.Compare(expected, actual);

        Assert.True(report.IsEmpty, report.ToString());
    }

    [Fact]
    public void Compare_ChilOrderSwapped_ReportsMismatch()
    {
        var expected =
            "0 @F1@ FAM\n" +
            "1 CHIL @I2@\n" +
            "1 CHIL @I3@\n" +
            "0 TRLR\n";

        var actual =
            "0 @F1@ FAM\n" +
            "1 CHIL @I3@\n" +
            "1 CHIL @I2@\n" +
            "0 TRLR\n";

        var report = GedcomSemanticDiff.Compare(expected, actual);

        // Prohozené CHIL se projeví jako dva ValueMismatch na CHIL[0]/CHIL[1]
        // (párování je i-tý výskyt tagu s i-tým výskytem).
        Assert.False(report.IsEmpty);
        Assert.All(report.Entries, e => Assert.Equal(GedcomDiffKind.ValueMismatch, e.Kind));
        Assert.Equal(2, report.Entries.Count);
    }
}
