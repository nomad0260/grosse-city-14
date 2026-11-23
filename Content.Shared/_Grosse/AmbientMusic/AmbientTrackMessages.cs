using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.AmbientMusic;

/// <summary>
///     Asks the server for one city ambience track.
/// </summary>
/// <remarks>
///     The tracks are not part of the client build: they live on the server and are sent to the client when the
///     round starts, so the repository itself contains no music.
/// </remarks>
[Serializable, NetSerializable]
public sealed class AmbientTrackRequestEvent(string name) : EntityEventArgs
{
    /// <summary>
    ///     File name of the track, e.g. <c>ambient_7.ogg</c>.
    /// </summary>
    public string Name { get; } = name;
}

/// <summary>
///     Piece of a track, sent in response to <see cref="AmbientTrackRequestEvent"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class AmbientTrackChunkEvent(string name, int index, int total, byte[] data) : EntityEventArgs
{
    /// <summary>
    ///     File name of the track the piece belongs to.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    ///     Position of the piece in the track, from zero.
    /// </summary>
    public int Index { get; } = index;

    /// <summary>
    ///     How many pieces the whole track consists of.
    /// </summary>
    public int Total { get; } = total;

    /// <summary>
    ///     Raw OGG Vorbis bytes of the piece.
    /// </summary>
    public byte[] Data { get; } = data;
}

/// <summary>
///     The server cannot serve a track: no directory is configured, the file is missing or it is not in the
///     sound collection.
/// </summary>
[Serializable, NetSerializable]
public sealed class AmbientTrackMissingEvent(string name) : EntityEventArgs
{
    /// <summary>
    ///     File name of the track that was asked for.
    /// </summary>
    public string Name { get; } = name;
}
