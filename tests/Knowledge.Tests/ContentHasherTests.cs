using Knowledge.Core.Abstractions;

namespace Knowledge.Tests;

public class ContentHasherTests
{
    [Fact]
    public void Hash_IsDeterministic()
    {
        Assert.Equal(ContentHasher.Hash("hello"), ContentHasher.Hash("hello"));
    }

    [Fact]
    public void Hash_ChangesWithContent()
    {
        Assert.NotEqual(ContentHasher.Hash("hello"), ContentHasher.Hash("world"));
    }

    [Fact]
    public void Hash_HandlesNull()
    {
        var hash = ContentHasher.Hash(null!);
        Assert.False(string.IsNullOrEmpty(hash));
    }
}
