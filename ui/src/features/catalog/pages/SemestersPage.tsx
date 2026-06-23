import { useEffect, useState, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { CatalogBreadcrumb } from "@/components/catalog/CatalogBreadcrumb";
import {
  CatalogEmptyState,
  CatalogErrorState,
  CatalogListSkeleton,
  CatalogPageShell,
} from "@/components/catalog/CatalogPageShell";
import { FilterBar, ContentBlockButton } from "@/components/ui/content-block";
import { ListPagination } from "@/components/catalog/ListPagination";
import { catalogService } from "@/services/catalogService";
import { adminCatalogService } from "@/services/adminCatalogService";
import { useCatalogPaths } from "@/features/catalog/hooks/useCatalogPaths";
import type { Semester } from "@/types/catalog";
import { isAxiosError } from "axios";
import {
  AdminModal,
  AdminField,
  AdminTextInput,
  AdminTextArea,
  AdminPrimaryButton,
  AdminSecondaryButton,
} from "@/features/admin/components/AdminModal";

const PAGE_SIZE = 10;

export function SemestersPage() {
  const [semesters, setSemesters] = useState<Semester[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeFilter, setActiveFilter] = useState<boolean | undefined>(undefined);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const navigate = useNavigate();
  const paths = useCatalogPaths();
  const isAdmin = paths.isAdminMode;

  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<Semester | null>(null);
  const [form, setForm] = useState({
    code: "",
    name: "",
    description: "",
    startDate: "",
    endDate: "",
    isActive: true,
  });
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const openCreate = () => {
    setEditing(null);
    setForm({
      code: "",
      name: "",
      description: "",
      startDate: "",
      endDate: "",
      isActive: true,
    });
    setFormError(null);
    setModalOpen(true);
  };

  const openEdit = (s: Semester, e: React.MouseEvent) => {
    e.stopPropagation();
    setEditing(s);
    setForm({
      code: s.code,
      name: s.name,
      description: s.description ?? "",
      startDate: s.startDate,
      endDate: s.endDate,
      isActive: s.isActive,
    });
    setFormError(null);
    setModalOpen(true);
  };

  const handleDelete = async (s: Semester, e: React.MouseEvent) => {
    e.stopPropagation();
    if (!confirm(`Xóa học kỳ "${s.code}" và toàn bộ kỳ thi/môn bên trong?`)) return;
    try {
      await adminCatalogService.deleteSemester(s.id);
      await load();
    } catch (err) {
      alert(
        isAxiosError(err)
          ? err.response?.data?.message ?? "Xóa học kỳ thất bại."
          : "Xóa học kỳ thất bại."
      );
    }
  };

  const handleSave = async () => {
    if (!form.code.trim() || !form.name.trim() || !form.startDate || !form.endDate) {
      setFormError("Vui lòng điền mã, tên và ngày bắt đầu/kết thúc.");
      return;
    }
    setSaving(true);
    setFormError(null);
    try {
      const payload = {
        code: form.code.trim(),
        name: form.name.trim(),
        description: form.description.trim() || undefined,
        startDate: form.startDate,
        endDate: form.endDate,
        isActive: form.isActive,
      };
      if (editing) {
        await adminCatalogService.updateSemester(editing.id, payload);
      } else {
        await adminCatalogService.createSemester(payload);
      }
      setModalOpen(false);
      await load();
    } catch (err) {
      setFormError(
        isAxiosError(err)
          ? err.response?.data?.message ?? "Lưu học kỳ thất bại."
          : "Lưu học kỳ thất bại."
      );
    } finally {
      setSaving(false);
    }
  };

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await catalogService.getSemesters(activeFilter, page, PAGE_SIZE);
      setSemesters(result.items);
      setTotalPages(result.totalPages);
      setTotalCount(result.totalCount);
    } catch (err) {
      const msg = isAxiosError(err)
        ? err.response?.status === 401
          ? "Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại."
          : `Không tải được danh sách học kỳ (${err.response?.status ?? "lỗi mạng"}).`
        : "Không tải được danh sách học kỳ.";
      setError(msg);
      setSemesters([]);
    } finally {
      setLoading(false);
    }
  }, [activeFilter, page]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <CatalogPageShell
      title="Học kỳ"
      subtitle="Chọn học kỳ để xem kỳ thi PE và các môn"
      breadcrumb={<CatalogBreadcrumb items={[{ label: "Danh mục thi" }, { label: "Học kỳ" }]} />}
      actions={
        isAdmin ? (
          <button
            type="button"
            onClick={openCreate}
            className="px-4 py-2 bg-primary text-primary-foreground rounded-lg text-sm font-medium hover:opacity-90"
          >
            + Tạo học kỳ
          </button>
        ) : undefined
      }
    >
      <FilterBar>
        {[
          { label: "Tất cả", value: undefined },
          { label: "Đang hoạt động", value: true },
          { label: "Đã kết thúc", value: false },
        ].map((opt) => (
          <button
            key={String(opt.value)}
            type="button"
            onClick={() => {
              setActiveFilter(opt.value);
              setPage(1);
            }}
            className={`px-4 py-2 rounded-lg text-sm font-medium transition-all border ${
              activeFilter === opt.value
                ? "bg-primary text-primary-foreground border-primary"
                : "bg-card text-ink-soft hover:bg-secondary hover:text-ink border-line"
            }`}
          >
            {opt.label}
          </button>
        ))}
      </FilterBar>

      {loading ? (
        <CatalogListSkeleton />
      ) : error ? (
        <CatalogErrorState message={error} onRetry={load} />
      ) : semesters.length === 0 ? (
        <CatalogEmptyState message="Chưa có học kỳ nào. Khởi động ExamCatalogService để seed dữ liệu." />
      ) : (
        <div className="grid gap-3">
          {semesters.map((s) => (
            <ContentBlockButton
              key={s.id}
              onClick={() => navigate(paths.semesterExams(s.id))}
              className="group"
            >
              <div className="flex items-center justify-between gap-4">
                <div className="flex items-center gap-4 min-w-0">
                  <div
                    className={`w-2.5 h-2.5 rounded-full shrink-0 ${
                      s.isActive ? "bg-done" : "bg-line"
                    }`}
                  />
                  <div className="min-w-0">
                    <h3 className="font-semibold group-hover:text-brand-red transition-colors truncate text-ink">
                      {s.code} — {s.name}
                    </h3>
                    {s.description && (
                      <p className="text-ink-soft text-sm mt-0.5 truncate">{s.description}</p>
                    )}
                  </div>
                </div>
                <div className="text-right text-sm text-ink-soft shrink-0 flex flex-col items-end gap-2">
                  <div>{s.startDate} → {s.endDate}</div>
                  <span
                    className={`inline-block px-2 py-0.5 rounded text-xs ${
                      s.isActive
                        ? "bg-done/10 text-done"
                        : "bg-secondary text-ink-soft"
                    }`}
                  >
                    {s.isActive ? "Active" : "Inactive"}
                  </span>
                  {isAdmin && (
                    <div className="flex gap-2">
                      <button
                        type="button"
                        onClick={(e) => openEdit(s, e)}
                        className="text-xs text-brand-red hover:underline"
                      >
                        Sửa
                      </button>
                      <button
                        type="button"
                        onClick={(e) => void handleDelete(s, e)}
                        className="text-xs text-destructive hover:underline"
                      >
                        Xóa
                      </button>
                    </div>
                  )}
                </div>
              </div>
            </ContentBlockButton>
          ))}
        </div>
      )}

      <ListPagination
        page={page}
        totalPages={totalPages}
        totalCount={totalCount}
        pageSize={PAGE_SIZE}
        onPageChange={setPage}
      />

      <AdminModal
        open={modalOpen}
        title={editing ? "Sửa học kỳ" : "Tạo học kỳ"}
        onClose={() => setModalOpen(false)}
        footer={
          <>
            <AdminSecondaryButton onClick={() => setModalOpen(false)}>
              Hủy
            </AdminSecondaryButton>
            <AdminPrimaryButton disabled={saving} onClick={() => void handleSave()}>
              {saving ? "Đang lưu…" : "Lưu"}
            </AdminPrimaryButton>
          </>
        }
      >
        {formError && <p className="text-sm text-destructive">{formError}</p>}
        <AdminField label="Mã học kỳ">
          <AdminTextInput
            value={form.code}
            onChange={(e) => setForm({ ...form, code: e.target.value })}
            placeholder="SP26"
          />
        </AdminField>
        <AdminField label="Tên">
          <AdminTextInput
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
          />
        </AdminField>
        <AdminField label="Mô tả">
          <AdminTextArea
            value={form.description}
            onChange={(e) => setForm({ ...form, description: e.target.value })}
          />
        </AdminField>
        <div className="grid grid-cols-2 gap-3">
          <AdminField label="Bắt đầu">
            <AdminTextInput
              type="date"
              value={form.startDate}
              onChange={(e) => setForm({ ...form, startDate: e.target.value })}
            />
          </AdminField>
          <AdminField label="Kết thúc">
            <AdminTextInput
              type="date"
              value={form.endDate}
              onChange={(e) => setForm({ ...form, endDate: e.target.value })}
            />
          </AdminField>
        </div>
        <label className="flex items-center gap-2 text-sm text-ink">
          <input
            type="checkbox"
            checked={form.isActive}
            onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
          />
          Đang hoạt động
        </label>
      </AdminModal>
    </CatalogPageShell>
  );
}
