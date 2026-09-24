using FluentAssertions;

namespace NextORM.Core.Tests;

public class LockRendererDefaultTests
{
    private sealed class WaitOnlyLockRenderer : ILockRenderer
    {
        public bool UsesTableHints => false;

        public string Render(LockMode mode, KeywordCase keywordCase = KeywordCase.Lower) =>
            mode == LockMode.Share ? " for share" : " for update";
    }

    [Fact]
    public void Render_Wait_ShouldDelegateToTheLegacyOverload()
    {
        ILockRenderer renderer = new WaitOnlyLockRenderer();

        renderer.Render(LockMode.Update, LockWaitMode.Wait).Should().Be(" for update");
        renderer.Render(LockMode.Share, LockWaitMode.Wait, KeywordCase.Upper).Should().Be(" for share");
    }

    [Fact]
    public void Render_NonWaitMode_ShouldThrowByDefault()
    {
        ILockRenderer renderer = new WaitOnlyLockRenderer();

        var act = () => renderer.Render(LockMode.Update, LockWaitMode.SkipLocked);

        act.Should().Throw<NotSupportedException>();
    }
}
