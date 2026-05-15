using System;
using System.Collections.Generic;
using System.Text;

namespace Contracts.Enums
{
    public enum AssignmentType
    {
        FirstGrade = 1,    // Chấm lần đầu
        CrossGrade = 2,    // Chấm chéo (nếu trường yêu cầu 2 người chấm 1 bài)
        ReGrade = 3        // Chấm phúc khảo
    }
}
