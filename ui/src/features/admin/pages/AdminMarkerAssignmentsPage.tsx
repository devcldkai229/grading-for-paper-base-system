import { useState, useEffect, useCallback } from "react";
import { useSearchParams } from "react-router-dom";
import { isAxiosError } from "axios";
import { Users, Search, Trash2, Edit2, Plus } from "lucide-react";
import { LecturerPageShell } from "@/components/layout/LecturerPageShell";
import { PageHeader } from "@/components/layout/PageHeader";
import { CatalogEmptyState, CatalogErrorState, CatalogListSkeleton } from "@/components/catalog/CatalogPageShell";
import {
  AdminModal,
  AdminField,
  AdminTextInput,
  AdminSelect,
  AdminPrimaryButton,
  AdminSecondaryButton,
} from "@/features/admin/components/AdminModal";
import { gradingService } from "@/services/gradingService";
import { catalogService } from "@/services/catalogService";
import { userService } from "@/services/userService";
import { UserRole } from "@/types/user";
import type { User } from "@/types/user";
import type { SubjectSearchResult } from "@/types/catalog";
import type { MarkerAssignment } from "@/types/grading";

type FormMode = "range" | "quota";

interface FormState {
  batchId: string;
  teacherId: string;
  mode: FormMode;
  aliasStart: string;
  aliasEnd: string;
  quota: string;
}

const emptyForm = (defaultTeacherId: string, defaultBatchId = ""): FormState => ({
  batchId: defaultBatchId,
  teacherId: defaultTeacherId,
  mode: "range",
  aliasStart: "",
  aliasEnd: "",
  quota: "",
});

export function AdminMarkerAssignmentsPage() {
  const [searchParams] = useSearchParams();
  const queryBatchId = searchParams.get("batchId") ?? "";
  const [subjectQuery, setSubjectQuery] = useState("");
  const [subjectResults, setSubjectResults] = useState<SubjectSearchResult[]>([]);
  const [searchingSubjects, setSearchingSubjects] = useState(false);
  const [selectedSubject, setSelectedSubject] = useState<SubjectSearchResult | null>(null);

  const [lecturers, setLecturers] = useState<User[]>([]);

  const [assignments, setAssignments] = useState<MarkerAssignment[]>([]);
  const [loadingAssignments, setLoadingAssignments] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [modalOpen, setModalOpen] = useState(false);
  const [editingAssignment, setEditingAssignment] = useState<MarkerAssignment | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm(""));
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  useEffect(() => {
    userService
      .getUsers(1, 200)
      .then((r) => setLecturers(r.items.filter((u) => u.role === UserRole.Lecturer)))
      .catch(() => setLecturers([]));
  }, []);

  useEffect(() => {
    const q = subjectQuery.trim();
    if (!q) {
      setSubjectResults([]);
      return;
    }
    setSearchingSubjects(true);
    const timer = setTimeout(() => {
      catalogService
        .searchSubjects({ code: q, pageSize: 8 })
        .then((r) => setSubjectResults(r.items))
        .catch(() => setSubjectResults([]))
        .finally(() => setSearchingSubjects(false));
    }, 350);
    return () => clearTimeout(timer);
  }, [subjectQuery]);

  const loadAssignments = useCallback(async (subjectId: string) => {
    setLoadingAssignments(true);
    setLoadError(null);
    try {
      const items = await gradingService.listMarkerAssignments(subjectId);
      setAssignments(items);
    } catch {
      setLoadError("Không tải được danh sách phân công.");
      setAssignments([]);
    } finally {
      setLoadingAssignments(false);
    }
  }, []);

  useEffect(() => {
    const subjectId = searchParams.get("subjectId");
    if (!subjectId) return;

    catalogService.getSubjectDetail(subjectId).then((subject) => {
      setSelectedSubject({
        id: subject.id,
        examId: subject.examId,
        examName: "",
        semesterId: "",
        semesterCode: "",
        subjectCode: subject.subjectCode,
        title: subject.title,
        maxScore: subject.maxScore,
        passScore: subject.passScore,
        status: subject.status,
        hasExamPaper: subject.hasExamPaper,
        hasRubric: subject.hasRubric,
        questionCount: subject.questions.length,
        createdAt: subject.createdAt,
      });
      void loadAssignments(subject.id);
    }).catch(() => setLoadError("Không tải được môn thi từ liên kết upload."));
  }, [searchParams, loadAssignments]);

  const selectSubject = (s: SubjectSearchResult) => {
    setSelectedSubject(s);
    setSubjectQuery("");
    setSubjectResults([]);
    void loadAssignments(s.id);
  };

  const lecturerLabel = (teacherId: string) => {
    const l = lecturers.find((u) => u.id === teacherId);
    return l ? l.fullName || l.email : teacherId;
  };

  const openCreate = () => {
    setEditingAssignment(null);
    setForm(emptyForm(lecturers[0]?.id ?? "", queryBatchId));
    setFormError(null);
    setModalOpen(true);
  };

  const openEdit = (a: MarkerAssignment) => {
    setEditingAssignment(a);
    setForm({
      batchId: a.batchId,
      teacherId: a.teacherId,
      mode: "range",
      aliasStart: String(a.aliasStart),
      aliasEnd: String(a.aliasEnd),
      quota: "",
    });
    setFormError(null);
    setModalOpen(true);
  };

  const handleSave = async () => {
    if (!selectedSubject) return;
    if (!form.teacherId) {
      setFormError("Vui lòng chọn giám khảo.");
      return;
    }
    if (!form.batchId) {
      setFormError("Vui lòng nhập Batch ID của file ZIP cần phân công.");
      return;
    }

    setSaving(true);
    setFormError(null);
    try {
      let saved: MarkerAssignment;
      if (editingAssignment) {
        const aliasStart = Number(form.aliasStart);
        const aliasEnd = Number(form.aliasEnd);
        if (!aliasStart || !aliasEnd || aliasEnd < aliasStart) {
          setFormError("Khoảng bí danh không hợp lệ.");
          return;
        }
        saved = await gradingService.reassignMarkerAssignment(editingAssignment.id, {
          batchId: form.batchId,
          teacherId: form.teacherId,
          aliasStart,
          aliasEnd,
        });
      } else if (form.mode === "range") {
        const aliasStart = Number(form.aliasStart);
        const aliasEnd = Number(form.aliasEnd);
        if (!aliasStart || !aliasEnd || aliasEnd < aliasStart) {
          setFormError("Khoảng bí danh không hợp lệ.");
          return;
        }
        saved = await gradingService.createMarkerAssignment(selectedSubject.id, {
          batchId: form.batchId,
          teacherId: form.teacherId,
          aliasStart,
          aliasEnd,
        });
      } else {
        const quota = Number(form.quota);
        if (!quota || quota < 1) {
          setFormError("Số lượng bài (quota) không hợp lệ.");
          return;
        }
        saved = await gradingService.createMarkerAssignment(selectedSubject.id, {
          batchId: form.batchId,
          teacherId: form.teacherId,
          quota,
        });
      }
      setSuccessMessage(
        `Đã tạo ${saved.materializedCount} bài trong hàng chờ của giảng viên.`
      );
      setModalOpen(false);
      await loadAssignments(selectedSubject.id);
    } catch (err) {
      setFormError(
        isAxiosError(err)
          ? err.response?.data?.message ?? "Lưu phân công thất bại."
          : "Lưu phân công thất bại."
      );
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (a: MarkerAssignment) => {
    if (!selectedSubject) return;
    const range = `Student_${String(a.aliasStart).padStart(4, "0")}–Student_${String(a.aliasEnd).padStart(4, "0")}`;
    if (!confirm(`Xóa phân công ${range} của ${lecturerLabel(a.teacherId)}?`)) return;
    try {
      await gradingService.deleteMarkerAssignment(a.id);
      await loadAssignments(selectedSubject.id);
    } catch {
      alert("Xóa phân công thất bại.");
    }
  };

  return (
    <LecturerPageShell maxWidth="5xl">
      <div className="flex flex-col gap-6">
        <PageHeader
          title="Phân phối bài cho giám khảo"
          subtitle="Chọn môn thi, phân bổ khoảng bí danh (alias) hoặc số lượng bài cho từng giám khảo — không được trùng lặp giữa các giám khảo."
        />

        {successMessage && (
          <div className="rounded-lg border border-done/30 bg-done/10 px-4 py-3 text-sm text-done">
            {successMessage}
          </div>
        )}

        <div className="relative">
          <Search className="absolute left-3 top-2.5 h-4 w-4 text-ink-soft" />
          <input
            type="text"
            value={
              selectedSubject
                ? `${selectedSubject.subjectCode}${selectedSubject.title ? ` — ${selectedSubject.title}` : ""}`
                : subjectQuery
            }
            onChange={(e) => {
              setSelectedSubject(null);
              setAssignments([]);
              setSubjectQuery(e.target.value);
            }}
            placeholder="Tìm môn thi theo mã..."
            className="w-full pl-9 pr-4 py-2 bg-card border border-line rounded-lg text-sm text-ink outline-none focus:border-primary placeholder:text-ink-soft/60"
          />
          {subjectResults.length > 0 && !selectedSubject && (
            <div className="absolute left-0 right-0 mt-1 bg-card border border-line rounded-lg shadow-lg z-10 max-h-64 overflow-y-auto">
              {subjectResults.map((s) => (
                <button
                  key={s.id}
                  type="button"
                  onClick={() => selectSubject(s)}
                  className="w-full text-left px-4 py-2 hover:bg-secondary/60 text-sm"
                >
                  <span className="font-medium text-ink">{s.subjectCode}</span>
                  {s.title && <span className="text-ink-soft"> — {s.title}</span>}
                  <span className="text-xs text-ink-soft block">
                    {s.semesterCode} · {s.examName}
                  </span>
                </button>
              ))}
            </div>
          )}
          {searchingSubjects && <p className="text-xs text-ink-soft mt-1">Đang tìm...</p>}
        </div>

        {selectedSubject && (
          <>
            <div className="flex items-center justify-between">
              <p className="text-sm text-ink-soft">
                Môn: <span className="font-semibold text-ink">{selectedSubject.subjectCode}</span>
              </p>
              <button
                type="button"
                onClick={openCreate}
                className="flex items-center gap-2 px-4 py-2 bg-primary text-primary-foreground rounded-lg text-sm font-medium hover:opacity-90 cursor-pointer shadow-sm"
              >
                <Plus className="h-4 w-4" />
                Phân công mới
              </button>
            </div>

            {loadingAssignments ? (
              <CatalogListSkeleton />
            ) : loadError ? (
              <CatalogErrorState message={loadError} onRetry={() => void loadAssignments(selectedSubject.id)} />
            ) : assignments.length === 0 ? (
              <CatalogEmptyState message="Môn thi này chưa có phân công nào." />
            ) : (
              <div className="bg-card border border-line rounded-xl overflow-hidden shadow-sm">
                <table className="w-full border-collapse text-left text-sm">
                  <thead className="bg-secondary/40 border-b border-line text-ink-soft font-medium">
                    <tr>
                      <th className="px-5 py-3">Giám khảo</th>
                      <th className="px-5 py-3">Batch</th>
                      <th className="px-5 py-3">Khoảng bí danh</th>
                      <th className="px-5 py-3">Số bài</th>
                      <th className="px-5 py-3">Ngày phân công</th>
                      <th className="px-5 py-3 text-right">Hành động</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-line text-ink">
                    {assignments.map((a) => (
                      <tr key={a.id} className="hover:bg-secondary/20 transition-colors">
                        <td className="px-5 py-4">
                          <div className="flex items-center gap-2">
                            <Users className="h-4 w-4 text-ink-soft shrink-0" />
                            {lecturerLabel(a.teacherId)}
                          </div>
                        </td>
                        <td className="px-5 py-4 font-mono text-xs">{a.batchId.slice(0, 8)}…</td>
                        <td className="px-5 py-4 font-mono text-xs">
                          Student_{String(a.aliasStart).padStart(4, "0")} – Student_
                          {String(a.aliasEnd).padStart(4, "0")}
                        </td>
                        <td className="px-5 py-4 font-score">{a.aliasEnd - a.aliasStart + 1}</td>
                        <td className="px-5 py-4 text-xs text-ink-soft">
                          {new Date(a.assignedAt).toLocaleString("vi-VN")}
                        </td>
                        <td className="px-5 py-4 text-right">
                          <div className="flex items-center justify-end gap-2">
                            <button
                              type="button"
                              onClick={() => openEdit(a)}
                              className="p-1 text-ink-soft hover:text-brand-red rounded hover:bg-secondary transition-all cursor-pointer"
                              title="Đổi giám khảo / khoảng bí danh"
                            >
                              <Edit2 className="h-4 w-4" />
                            </button>
                            <button
                              type="button"
                              onClick={() => void handleDelete(a)}
                              className="p-1 text-ink-soft hover:text-destructive rounded hover:bg-secondary transition-all cursor-pointer"
                              title="Xóa phân công"
                            >
                              <Trash2 className="h-4 w-4" />
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </>
        )}
      </div>

      <AdminModal
        open={modalOpen}
        title={editingAssignment ? "Đổi giám khảo / khoảng bí danh" : "Phân công mới"}
        onClose={() => setModalOpen(false)}
        footer={
          <>
            <AdminSecondaryButton onClick={() => setModalOpen(false)}>Hủy</AdminSecondaryButton>
            <AdminPrimaryButton disabled={saving} onClick={() => void handleSave()}>
              {saving ? "Đang lưu..." : "Lưu"}
            </AdminPrimaryButton>
          </>
        }
      >
        {formError && <p className="text-sm text-destructive font-medium">{formError}</p>}

        <AdminField label="Batch ID">
          <AdminTextInput
            value={form.batchId}
            onChange={(e) => setForm({ ...form, batchId: e.target.value })}
            placeholder="UUID của batch đã upload"
          />
        </AdminField>

        <AdminField label="Giám khảo">
          <AdminSelect
            value={form.teacherId}
            onChange={(e) => setForm({ ...form, teacherId: e.target.value })}
          >
            <option value="">— Chọn giám khảo —</option>
            {lecturers.map((l) => (
              <option key={l.id} value={l.id}>
                {l.fullName || l.email}
              </option>
            ))}
          </AdminSelect>
        </AdminField>

        {!editingAssignment && (
          <div className="flex rounded-lg border border-line bg-secondary/40 p-1 gap-1">
            <button
              type="button"
              onClick={() => setForm({ ...form, mode: "range" })}
              className={`flex-1 py-1.5 rounded-md text-sm font-medium transition-colors ${
                form.mode === "range"
                  ? "bg-card text-ink shadow-sm border border-line"
                  : "text-ink-soft hover:text-ink"
              }`}
            >
              Theo khoảng
            </button>
            <button
              type="button"
              onClick={() => setForm({ ...form, mode: "quota" })}
              className={`flex-1 py-1.5 rounded-md text-sm font-medium transition-colors ${
                form.mode === "quota"
                  ? "bg-card text-ink shadow-sm border border-line"
                  : "text-ink-soft hover:text-ink"
              }`}
            >
              Theo số lượng
            </button>
          </div>
        )}

        {editingAssignment || form.mode === "range" ? (
          <div className="grid grid-cols-2 gap-3">
            <AdminField label="Bí danh bắt đầu">
              <AdminTextInput
                type="number"
                min={1}
                value={form.aliasStart}
                onChange={(e) => setForm({ ...form, aliasStart: e.target.value })}
                placeholder="1"
              />
            </AdminField>
            <AdminField label="Bí danh kết thúc">
              <AdminTextInput
                type="number"
                min={1}
                value={form.aliasEnd}
                onChange={(e) => setForm({ ...form, aliasEnd: e.target.value })}
                placeholder="20"
              />
            </AdminField>
          </div>
        ) : (
          <>
            <AdminField label="Số lượng bài (quota)">
              <AdminTextInput
                type="number"
                min={1}
                value={form.quota}
                onChange={(e) => setForm({ ...form, quota: e.target.value })}
                placeholder="20"
              />
            </AdminField>
            <p className="text-xs text-ink-soft">
              Hệ thống sẽ tự động chọn khoảng bí danh tiếp theo còn trống cho môn này.
            </p>
          </>
        )}
      </AdminModal>
    </LecturerPageShell>
  );
}
