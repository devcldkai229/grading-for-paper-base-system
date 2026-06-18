using System.Runtime.Serialization;

namespace SubmissionService.Domain.Enums;

public enum BatchStatus
{
    [EnumMember(Value = "uploaded")]
    Uploaded,

    [EnumMember(Value = "extracting")]
    Extracting,

    [EnumMember(Value = "ready")]
    Ready,

    [EnumMember(Value = "failed")]
    Failed
}
