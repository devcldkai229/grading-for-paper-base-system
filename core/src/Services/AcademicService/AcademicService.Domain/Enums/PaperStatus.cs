using System;
using System.Collections.Generic;
using System.Text;

namespace AcademicService.Domain.Enums
{
    public enum PaperStatus
    {
        Imported = 0,      // Mới upload file/folder vào hệ thống
        Processing = 1,    // Đang được Document Service extract gộp file
        ReadyToAssign = 2, // Đã extract xong, sẵn sàng phân công
        Assigned = 3,      // Đã chia cho giáo viên nhưng chưa chấm xong
        Completed = 4,     // Tất cả giáo viên được phân công đã submit
        ReGrading = 5      // Đang trong trạng thái có yêu cầu phúc khảo
    }
}
