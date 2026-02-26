using GoogDocsLite.Shared;
using System.ComponentModel.DataAnnotations;

namespace GoogDocsLite.Server.Contracts.Documents;

public class ApplyLiveContentRequest
{
    [MaxLength(AppDefaults.DocumentTitleMaxLength)]
    public string? Title { get; init; }

    [Required]
    public string ContentHtml { get; init; } = string.Empty;

    public string? ContentDeltaJson { get; init; }

    public long ClientSequence { get; init; }
}
