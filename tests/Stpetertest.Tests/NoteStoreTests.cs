using Stpetertest.Domain;
using Xunit;

namespace Stpetertest.Tests;

// The rules of the resource, without the host. The endpoint tests drive the
// same code through HTTP; both are needed, because one proves the rule and the
// other proves the rule is actually wired to a route.
public class NoteStoreTests
{
    [Fact]
    public void Add_ThenFind_ReturnsTheSameNote()
    {
        var store = new InMemoryNoteStore();

        var created = store.Add("Shipping", "Notes about shipping");

        Assert.Equal(created, store.Find(created.Id));
    }

    [Fact]
    public void Find_ReturnsNull_ForAnUnknownId()
    {
        var store = new InMemoryNoteStore();

        Assert.Null(store.Find(Guid.NewGuid()));
    }

    [Fact]
    public void Replace_KeepsTheOriginalCreatedAt()
    {
        // An edit is not a new note. Without this, updating one would reorder
        // the list and a client paging by age would see it jump the queue.
        var store = new InMemoryNoteStore();
        var created = store.Add("Before", "body");

        var updated = store.Replace(created.Id, "After", "new body");

        Assert.NotNull(updated);
        Assert.Equal("After", updated!.Title);
        Assert.Equal("new body", updated.Body);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
        Assert.Equal(created.Id, updated.Id);
    }

    [Fact]
    public void Replace_ReturnsNull_WhenTheNoteIsGone()
    {
        var store = new InMemoryNoteStore();

        Assert.Null(store.Replace(Guid.NewGuid(), "Title", "Body"));
    }

    [Fact]
    public void Remove_ReportsWhetherItActuallyRemovedSomething()
    {
        var store = new InMemoryNoteStore();
        var created = store.Add("Temporary", string.Empty);

        Assert.True(store.Remove(created.Id));
        // The second call must report false. A delete that reports success for
        // a note that was never there hides a client bug.
        Assert.False(store.Remove(created.Id));
    }

    [Fact]
    public void All_ReturnsNotesOldestFirst()
    {
        var store = new InMemoryNoteStore();
        var first = store.Add("First", string.Empty);
        var second = store.Add("Second", string.Empty);

        Assert.Equal([first.Id, second.Id], store.All().Select(note => note.Id));
    }

    [Fact]
    public void All_IsEmpty_ForANewStore()
    {
        Assert.Empty(new InMemoryNoteStore().All());
    }
}
