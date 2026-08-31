using System.Collections.Generic;
using System.Xml.Serialization;

namespace MogFlix.Services;

// Plex's /status/sessions endpoint returns XML by default, e.g.:
// <MediaContainer size="1">
//   <Video type="movie" title="Some Movie" year="2019" duration="7200000" viewOffset="450000">
//     <User title="sadonia" />
//     <Player state="playing" title="Living Room TV" />
//   </Video>
// </MediaContainer>

[XmlRoot("MediaContainer")]
public class MediaContainer
{
    [XmlElement("Video")]
    public List<PlexVideo> Videos { get; set; } = new();

    // Music sessions come back as <Track> elements, not <Video>.
    [XmlElement("Track")]
    public List<PlexTrack> Tracks { get; set; } = new();
}

public class PlexVideo
{
    [XmlAttribute("title")] public string Title { get; set; } = "";

    // Only present for TV episodes - the show's title.
    [XmlAttribute("grandparentTitle")] public string? ShowTitle { get; set; }

    [XmlAttribute("type")] public string Type { get; set; } = ""; // "movie" or "episode"
    [XmlAttribute("year")] public string? Year { get; set; }
    [XmlAttribute("duration")] public long DurationMs { get; set; }
    [XmlAttribute("viewOffset")] public long ViewOffsetMs { get; set; }
    [XmlAttribute("thumb")] public string? Thumb { get; set; }

    [XmlElement("User")] public PlexUser? User { get; set; }
    [XmlElement("Player")] public PlexPlayer? Player { get; set; }
}

public class PlexTrack
{
    [XmlAttribute("title")] public string Title { get; set; } = "";

    // Artist name.
    [XmlAttribute("grandparentTitle")] public string? ArtistName { get; set; }

    // Album name.
    [XmlAttribute("parentTitle")] public string? AlbumName { get; set; }

    [XmlAttribute("duration")] public long DurationMs { get; set; }
    [XmlAttribute("viewOffset")] public long ViewOffsetMs { get; set; }

    [XmlElement("User")] public PlexUser? User { get; set; }
    [XmlElement("Player")] public PlexPlayer? Player { get; set; }
}

public class PlexUser
{
    [XmlAttribute("title")] public string Title { get; set; } = "";
}

public class PlexPlayer
{
    [XmlAttribute("state")] public string State { get; set; } = ""; // playing / paused / buffering
    [XmlAttribute("title")] public string DeviceTitle { get; set; } = "";
}
