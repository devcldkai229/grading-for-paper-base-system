using System;
using System.Collections.Generic;
using System.Text;

namespace Contracts.Enums
{
    public enum GradingProgressStatus
    {
        NotStarted = 0,    // Chưa đụng vào
        Drafting = 1,      // Đang chấm dở (đang lưu nháp)
        Submitted = 2      // Đã chốt điểm (chỉ khi status này thì nút Next mới hoạt động chuẩn)
    }
}
