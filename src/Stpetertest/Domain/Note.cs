using System.Collections.Concurrent;

namespace Stpetertest.Domain;

public sealed record Note(Guid Id, string Title, string Body, DateTimeOffset CreatedAt);

// The transport layer asks for this, so a test can substitute it without
// standing up the host — the same seam IServiceStatus provides.
public interface INoteStore
{
    IReadOnlyCollection<Note> All();

    Note? Find(Guid id);

    Note Add(string title, string body);

    Note? Replace(Guid id, string title, string body);

    bool Remove(Guid id);
}

/// <summary>
/// Replace this with a real repository when the service gets a database.
/// Entries live for the lifetime of the process and are lost on restart, which
/// is fine for a starting point and is not fine for anything else.
/// </summary>
public sealed class InMemoryNoteStore : INoteStore
{
    private readonly ConcurrentDictionary<Guid, Note> _notes = new();

    public IReadOnlyCollection<Note> All() =>
        _notes.Values.OrderBy(note => note.CreatedAt).ToList();

    public Note? Find(Guid id) => _notes.TryGetValue(id, out var note) ? note : null;

    public Note Add(string title, string body)
    {
        var note = new Note(Guid.NewGuid(), title, body, DateTimeOffset.UtcNow);
        _notes[note.Id] = note;
        return note;
    }

    public Note? Replace(Guid id, string title, string body)
    {
        if (!_notes.TryGetValue(id, out var existing))
        {
            return null;
        }

        // CreatedAt is preserved: an update is not a new note, and a client
        // paging by age would otherwise see edited notes jump the queue.
        var updated = existing with { Title = title, Body = body };
        _notes[id] = updated;
        return updated;
    }

    public bool Remove(Guid id) => _notes.TryRemove(id, out _);
}
