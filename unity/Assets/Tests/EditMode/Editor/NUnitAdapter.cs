#if UNITY_EDITOR && GUNYA_NUNIT_TESTS
// 既存環境にNUnitがある場合だけ有効化する任意のアダプター。
// テストパッケージがない環境でも上のメニューから同じ検証を実行できる。
using NUnit.Framework;

public class GunyaEditModeTests
{
    [Test]
    public void CatalogGachaEconomyAndMatchRules()
    {
        Assert.AreEqual(11, RegressionChecks.RunAll(null));
    }
}
#endif
