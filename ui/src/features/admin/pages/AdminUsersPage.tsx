import { useEffect, useState, useCallback } from "react";
import { LecturerPageShell } from "@/components/layout/LecturerPageShell";
import { PageHeader } from "@/components/layout/PageHeader";
import { ListPagination } from "@/components/catalog/ListPagination";
import { CatalogEmptyState, CatalogErrorState, CatalogListSkeleton } from "@/components/catalog/CatalogPageShell";
import { userService } from "@/services/userService";
import { UserRole, UserStatus } from "@/types/user";
import type { User } from "@/types/user";
import { isAxiosError } from "axios";
import {
  AdminModal,
  AdminField,
  AdminTextInput,
  AdminSelect,
  AdminPrimaryButton,
  AdminSecondaryButton,
} from "@/features/admin/components/AdminModal";
import { Shield, UserPlus, Search, Edit2, Trash2, Mail, Award, Circle } from "lucide-react";

const PAGE_SIZE = 10;

export function AdminUsersPage() {
  const [users, setUsers] = useState<User[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);

  // Modal & form state
  const [modalOpen, setModalOpen] = useState(false);
  const [editingUser, setEditingUser] = useState<User | null>(null);
  const [form, setForm] = useState({
    email: "",
    password: "",
    fullName: "",
    phoneNumber: "",
    markerCode: "",
    role: UserRole.Lecturer as UserRole,
    status: UserStatus.Active as UserStatus,
  });
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await userService.getUsers(page, PAGE_SIZE, search.trim() || undefined);
      setUsers(result.items);
      setTotalPages(result.totalPages);
      setTotalCount(result.totalCount);
    } catch (err) {
      setError("Không tải được danh sách tài khoản. Vui lòng thử lại.");
      setUsers([]);
    } finally {
      setLoading(false);
    }
  }, [page, search]);

  useEffect(() => {
    load();
  }, [load]);

  const openCreate = () => {
    setEditingUser(null);
    setForm({
      email: "",
      password: "",
      fullName: "",
      phoneNumber: "",
      markerCode: "",
      role: UserRole.Lecturer as UserRole,
      status: UserStatus.Active as UserStatus,
    });
    setFormError(null);
    setModalOpen(true);
  };

  const openEdit = (u: User) => {
    setEditingUser(u);
    setForm({
      email: u.email,
      password: "", // do not populate password on edit
      fullName: u.fullName ?? "",
      phoneNumber: u.phoneNumber ?? "",
      markerCode: u.markerCode ?? "",
      role: u.role,
      status: u.status,
    });
    setFormError(null);
    setModalOpen(true);
  };

  const handleSave = async () => {
    if (!form.email.trim() || !form.fullName.trim() || (!editingUser && !form.password)) {
      setFormError("Vui lòng điền đầy đủ email, họ tên và mật khẩu.");
      return;
    }
    setSaving(true);
    setFormError(null);
    try {
      if (editingUser) {
        await userService.updateUser(editingUser.id, {
          fullName: form.fullName.trim(),
          phoneNumber: form.phoneNumber.trim() || undefined,
          markerCode: form.markerCode.trim() || undefined,
          role: form.role,
          status: form.status,
        });
      } else {
        await userService.createUser({
          email: form.email.trim(),
          password: form.password,
          fullName: form.fullName.trim(),
          phoneNumber: form.phoneNumber.trim() || undefined,
          markerCode: form.markerCode.trim() || undefined,
          role: form.role,
        });
      }
      setModalOpen(false);
      await load();
    } catch (err) {
      setFormError(
        isAxiosError(err)
          ? err.response?.data?.message ?? "Lưu thông tin thất bại."
          : "Lưu thông tin thất bại."
      );
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (u: User) => {
    if (
      !confirm(
        `Bạn có chắc chắn muốn xóa tài khoản "${u.email}"? \nHệ thống sẽ tự động chuyển sang xóa mềm nếu tài khoản này đã có dữ liệu thi cử/chấm điểm.`
      )
    )
      return;
    try {
      await userService.deleteUser(u.id);
      await load();
    } catch (err) {
      alert("Xóa tài khoản thất bại.");
    }
  };

  return (
    <LecturerPageShell maxWidth="5xl">
      <div className="flex flex-col gap-6">
        <PageHeader
          title="Quản lý tài khoản"
          subtitle="Quản trị viên CRUD, tìm kiếm, phân quyền, kích hoạt/khóa tài khoản giảng viên."
          actions={
            <button
              type="button"
              onClick={openCreate}
              className="flex items-center gap-2 px-4 py-2 bg-primary text-primary-foreground rounded-lg text-sm font-medium hover:opacity-90 cursor-pointer shadow-sm"
            >
              <UserPlus className="h-4 w-4" />
              Tạo tài khoản
            </button>
          }
        />

        {/* Search Bar */}
        <div className="relative">
          <Search className="absolute left-3 top-2.5 h-4 w-4 text-ink-soft" />
          <input
            type="text"
            placeholder="Tìm kiếm tài khoản theo email, họ tên, mã marker..."
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
            className="w-full pl-9 pr-4 py-2 bg-card border border-line rounded-lg text-sm text-ink outline-none focus:border-primary placeholder:text-ink-soft/60"
          />
        </div>

        {/* Content */}
        {loading ? (
          <CatalogListSkeleton />
        ) : error ? (
          <CatalogErrorState message={error} onRetry={load} />
        ) : users.length === 0 ? (
          <CatalogEmptyState message="Không tìm thấy tài khoản nào phù hợp." />
        ) : (
          <div className="bg-card border border-line rounded-xl overflow-hidden shadow-sm">
            <table className="w-full border-collapse text-left text-sm">
              <thead className="bg-secondary/40 border-b border-line text-ink-soft font-medium">
                <tr>
                  <th className="px-5 py-3">Thông tin tài khoản</th>
                  <th className="px-5 py-3">Vai trò</th>
                  <th className="px-5 py-3">Mã Marker</th>
                  <th className="px-5 py-3">Trạng thái</th>
                  <th className="px-5 py-3 text-right">Hành động</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line text-ink">
                {users.map((u) => (
                  <tr key={u.id} className="hover:bg-secondary/20 transition-colors">
                    <td className="px-5 py-4">
                      <div className="flex items-center gap-3">
                        <div className="h-9 w-9 rounded-full bg-secondary border border-line flex items-center justify-center font-display font-semibold text-brand-red shrink-0">
                          {u.fullName?.charAt(0).toUpperCase() ?? "U"}
                        </div>
                        <div className="min-w-0">
                          <p className="font-semibold truncate">{u.fullName ?? "Chưa thiết lập"}</p>
                          <p className="text-xs text-ink-soft flex items-center gap-1 mt-0.5 truncate">
                            <Mail className="h-3 w-3 inline shrink-0" />
                            {u.email}
                          </p>
                        </div>
                      </div>
                    </td>
                    <td className="px-5 py-4">
                      <span
                        className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold border ${
                          u.role === UserRole.Admin
                            ? "bg-brand-red/10 text-brand-red border-brand-red/20"
                            : "bg-secondary text-ink-soft border-line"
                        }`}
                      >
                        <Shield className="h-3 w-3" />
                        {u.role === UserRole.Admin ? "Admin" : "Giảng viên"}
                      </span>
                    </td>
                    <td className="px-5 py-4 font-mono text-xs text-ink-soft">
                      {u.markerCode ? (
                        <span className="flex items-center gap-1.5">
                          <Award className="h-3.5 w-3.5 text-brand-orange" />
                          {u.markerCode}
                        </span>
                      ) : (
                        "—"
                      )}
                    </td>
                    <td className="px-5 py-4">
                      <span className="flex items-center gap-1.5 text-xs font-medium">
                        <Circle
                          className={`h-2 w-2 fill-current ${
                            u.status === UserStatus.Active
                              ? "text-done"
                              : u.status === UserStatus.Inactive
                              ? "text-ink-soft"
                              : "text-destructive"
                          }`}
                        />
                        {u.status === UserStatus.Active
                          ? "Đang hoạt động"
                          : u.status === UserStatus.Inactive
                          ? "Ngưng hoạt động"
                          : "Đã khóa"}
                      </span>
                    </td>
                    <td className="px-5 py-4 text-right">
                      <div className="flex items-center justify-end gap-2">
                        <button
                          type="button"
                          onClick={() => openEdit(u)}
                          className="p-1 text-ink-soft hover:text-brand-red rounded hover:bg-secondary transition-all cursor-pointer"
                          title="Sửa thông tin"
                        >
                          <Edit2 className="h-4 w-4" />
                        </button>
                        <button
                          type="button"
                          onClick={() => void handleDelete(u)}
                          className="p-1 text-ink-soft hover:text-destructive rounded hover:bg-secondary transition-all cursor-pointer"
                          title="Xóa tài khoản"
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

        {/* Pagination */}
        <ListPagination
          page={page}
          totalPages={totalPages}
          totalCount={totalCount}
          pageSize={PAGE_SIZE}
          onPageChange={setPage}
        />
      </div>

      {/* Admin modal for create/edit */}
      <AdminModal
        open={modalOpen}
        title={editingUser ? "Sửa tài khoản" : "Tạo tài khoản mới"}
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

        <AdminField label="Địa chỉ Email">
          <AdminTextInput
            type="email"
            value={form.email}
            disabled={!!editingUser}
            onChange={(e) => setForm({ ...form, email: e.target.value })}
            placeholder="giangvien@example.com"
          />
        </AdminField>

        {!editingUser && (
          <AdminField label="Mật khẩu khởi tạo">
            <AdminTextInput
              type="password"
              value={form.password}
              onChange={(e) => setForm({ ...form, password: e.target.value })}
              placeholder="••••••••"
            />
          </AdminField>
        )}

        <AdminField label="Họ và tên hiển thị">
          <AdminTextInput
            value={form.fullName}
            onChange={(e) => setForm({ ...form, fullName: e.target.value })}
            placeholder="Nguyễn Văn A"
          />
        </AdminField>

        <div className="grid grid-cols-2 gap-3">
          <AdminField label="Số điện thoại">
            <AdminTextInput
              value={form.phoneNumber}
              onChange={(e) => setForm({ ...form, phoneNumber: e.target.value })}
              placeholder="0912345678"
            />
          </AdminField>
          <AdminField label="Mã Marker (GV)">
            <AdminTextInput
              value={form.markerCode}
              onChange={(e) => setForm({ ...form, markerCode: e.target.value })}
              placeholder="HungLD5"
            />
          </AdminField>
        </div>

        <AdminField label="Vai trò">
          <AdminSelect
            value={form.role}
            onChange={(e) => setForm({ ...form, role: Number(e.target.value) as UserRole })}
          >
            <option value={UserRole.Lecturer}>Giảng viên (Lecturer)</option>
            <option value={UserRole.Admin}>Quản trị viên (Admin)</option>
          </AdminSelect>
        </AdminField>

        {editingUser && (
          <AdminField label="Trạng thái tài khoản">
            <AdminSelect
              value={form.status}
              onChange={(e) => setForm({ ...form, status: Number(e.target.value) as UserStatus })}
            >
              <option value={UserStatus.Active}>Đang hoạt động (Active)</option>
              <option value={UserStatus.Inactive}>Ngưng hoạt động (Inactive)</option>
              <option value={UserStatus.Locked}>Bị khóa (Locked)</option>
            </AdminSelect>
          </AdminField>
        )}
      </AdminModal>
    </LecturerPageShell>
  );
}
