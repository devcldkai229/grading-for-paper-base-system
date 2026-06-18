using System.Runtime.Serialization;

namespace SubmissionService.Domain.Enums;

public enum PaperStatus
{
    [EnumMember(Value = "ready_to_assign")]
    ReadyToAssign,

    [EnumMember(Value = "assigned")]
    Assigned,

    [EnumMember(Value = "completed")]
    Completed,

    [EnumMember(Value = "re_grading")]
    ReGrading
}
