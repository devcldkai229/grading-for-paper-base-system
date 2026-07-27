import { useState, useEffect, useCallback, useRef } from "react";
import { isAxiosError } from "axios";
import { Users, Search, Trash2, Plus, Upload, FolderOpen } from "lucide-react";
import { LecturerPageShell } from "@/components/layout/LecturerPageShell";
import { PageHeader } from "@/components/layout/PageHeader";
import { CatalogEmptyState, CatalogErrorState, CatalogListSkeleton } from "@/components/catalog/CatalogPageShell";
import {
  AdminModal,
  AdminField,
  AdminSelect,
  AdminPrimaryButton,
  AdminSecondaryButton,
} from "@/features/admin/components/AdminModal";
import { gradingService } from "@/services/gradingService";
import { submissionService } from "@/services/submissionService";
import { catalogService } from "@/services/catalogService";
import { userService } from "@/services/userService";
import { UserRole } from "@/types/user";
import type { User } from "@/types/user";
import type { SubjectSearchResult } from "@/types/catalog";
import type { MarkerAssignment } from "@/types/grading";

const POLL_MS = 2000;

function assignmentLabel(a: MarkerAssignment): string {
  if (a.zipFileName) return a.zipFileName;
  if (a.batchId) return `Batch ${a.batchId.slice(0, 8)}…`;
  return `Student_${String(a.aliasStart).padStart(4, "0")} – Student_${String(a.aliasEnd).padStart(4, "0")}`;
}

export function AdminMarkerAssignmentsPage() {
  const [subjectQuery, setSubjectQuery] = useState("");
  const [subjectResults, setSubjectResults] = useState<SubjectSearchResult[]>([]);
  const [searchingSubjects, setSearchingSubjects] = useState(false);
  const [selectedSubject, setSelectedSubject] = useState<SubjectSearchResult | null>(null);

  const [lecturers, setLecturers] = useState<User[]>([]);

  const [assignments, setAssignments] = useState<MarkerAssignment[]>([]);
  const [loadingAssignments, setLoadingAssignments] = useState(false);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [modalOpen, setModalOpen] = useState(false);
  const [teacherId, setTeacherId] = useState("");
  const [zipFile, setZipFile] = useState<File | null>(null);
  const [uploadStatus, setUploadStatus] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

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

  const selectSubject = (s: SubjectSearchResult) => {
    setSelectedSubject(s);
    setSubjectQuery("");
    setSubjectResults([]);
    void loadAssignments(s.id);
  };

  const lecturerLabel = (id: string) => {
    const l = lecturers.find((u) => u.id === id);
    return l ? l.fullName || l.email : id;
  };

  const openCreate = () => {
    setTeacherId(lecturers[0]?.id ?? "");
    setZipFile(null);
    setUploadStatus(null);
    setFormError(null);
    setModalOpen(true);
  };

  const pollBatchReady = async (batchId: string): Promise<void> => {
    for (;;) {
      const status = await submissionService.getBatchStatus(batchId);
      if (status.status === "Ready") return;
      if (status.status === "Failed") {
        throw new Error(status.errorMessage ?? "Xử lý ZIP thất bại.");
      }
      await new Promise((r) => setTimeout(r, POLL_MS));
    }
  };

  const handleSave = async () => {
    if (!selectedSubject) return;
    if (!teacherId) {
      setFormError("Vui lòng chọn giám khảo.");
      return;
    }
    if (!zipFile) {
      setFormError("Vui lòng chọn file ZIP chứa bài làm học sinh.");
      return;
    }

    setSaving(true);
    setFormError(null);
    setSuccessMessage(null);
    setUploadStatus("Đang upload ZIP…");

    try {
      const { batchId } = await submissionService.uploadBatch(selectedSubject.id, zipFile);
      setUploadStatus("Đang xử lý ZIP…");
      await pollBatchReady(batchId);

      setUploadStatus("Đang phân công cho giảng viên…");
      const result = await gradingService.createFolderAssignment(selectedSubject.id, {
        teacherId,
        batchId,
        zipFileName: zipFile.name,
      });

      const label = zipFile.name;
      const parts = [
        `Đã giao folder "${label}" cho ${lecturerLabel(teacherId)} — ${result.materializedCount} bài.`,
      ];
      if (result.skippedInProgressCount > 0) {
        parts.push(`${result.skippedInProgressCount} bài bỏ qua (đang chấm).`);
      }
      if (result.warnings?.length) {
        parts.push(...result.warnings);
      }
      setSuccessMessage(parts.join(" "));
      setModalOpen(false);
      await loadAssignments(selectedSubject.id);
    } catch (err) {
      setFormError(
        isAxiosError(err)
          ? err.response?.data?.message ?? "Phân công folder thất bại."
          : err instanceof Error
            ? err.message
            : "Phân công folder thất bại."
      );
    } finally {
      setSaving(false);
      setUploadStatus(null);
    }
  };

  const handleDelete = async (a: MarkerAssignment) => {
    if (!selectedSubject) return;
    const label = assignmentLabel(a);
    if (!confirm(`Xóa phân công "${label}" của ${lecturerLabel(a.teacherId)}?`)) return;
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
          subtitle="Chọn môn thi, chọn giảng viên và upload folder ZIP bài làm — giảng viên sẽ nhận thông báo ngay khi được giao."
        />

        {successMessage && (
          <div className="p-3 bg-done/10 border border-done/25 rounded-lg text-done text-sm">
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
                      <th className="px-5 py-3">Folder / khoảng</th>
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
                        <td className="px-5 py-4">
                          <div className="flex items-center gap-2">
                            {a.batchId && <FolderOpen className="h-4 w-4 text-ink-soft shrink-0" />}
                            <span className={a.batchId ? "font-medium" : "font-mono text-xs"}>
                              {assignmentLabel(a)}
                            </span>
                          </div>
                        </td>
                        <td className="px-5 py-4 font-score">{a.aliasEnd - a.aliasStart + 1}</td>
                        <td className="px-5 py-4 text-xs text-ink-soft">
                          {new Date(a.assignedAt).toLocaleString("vi-VN")}
                        </td>
                        <td className="px-5 py-4 text-right">
                          <button
                            type="button"
                            onClick={() => void handleDelete(a)}
                            className="p-1 text-ink-soft hover:text-destructive rounded hover:bg-secondary transition-all cursor-pointer"
                            title="Xóa phân công"
                          >
                            <Trash2 className="h-4 w-4" />
                          </button>
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
        title="Phân công mới"
        onClose={() => !saving && setModalOpen(false)}
        footer={
          <>
            <AdminSecondaryButton disabled={saving} onClick={() => setModalOpen(false)}>
              Hủy
            </AdminSecondaryButton>
            <AdminPrimaryButton disabled={saving} onClick={() => void handleSave()}>
              {saving ? "Đang xử lý…" : "Giao folder"}
            </AdminPrimaryButton>
          </>
        }
      >
        {formError && <p className="text-sm text-destructive font-medium">{formError}</p>}
        {uploadStatus && <p className="text-sm text-ink-soft">{uploadStatus}</p>}

        <AdminField label="Giám khảo">
          <AdminSelect
            value={teacherId}
            onChange={(e) => setTeacherId(e.target.value)}
            disabled={saving}
          >
            <option value="">— Chọn giám khảo —</option>
            {lecturers.map((l) => (
              <option key={l.id} value={l.id}>
                {l.fullName || l.email}
              </option>
            ))}
          </AdminSelect>
        </AdminField>

        <AdminField label="Folder ZIP bài làm">
          <input
            ref={fileInputRef}
            type="file"
            accept=".zip,application/zip"
            className="hidden"
            onChange={(e) => setZipFile(e.target.files?.[0] ?? null)}
          />
          <button
            type="button"
            disabled={saving}
            onClick={() => fileInputRef.current?.click()}
            className="w-full flex flex-col items-center justify-center gap-2 rounded-lg border-2 border-dashed border-line bg-secondary/30 px-4 py-8 text-sm text-ink-soft hover:border-primary hover:bg-secondary/50 transition-colors"
          >
            <Upload className="h-8 w-8 text-ink-soft" />
            {zipFile ? (
              <span className="font-medium text-ink">{zipFile.name}</span>
            ) : (
              <>
                <span>Nhấn để chọn file ZIP</span>
                <span className="text-xs">Mỗi thư mục con hoặc file = một bài làm</span>
              </>
            )}
          </button>
        </AdminField>
      </AdminModal>
    </LecturerPageShell>
  );
}
