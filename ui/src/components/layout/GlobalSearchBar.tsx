import { useState, useEffect, useRef, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { Search, X } from "lucide-react";
import { authService } from "@/services/authService";
import { catalogService } from "@/services/catalogService";
import { submissionService } from "@/services/submissionService";
import { userService } from "@/services/userService";
import type { SubjectSearchResult } from "@/types/catalog";
import type { StudentPaper } from "@/types/submission";
import type { User } from "@/types/user";

const DEBOUNCE_MS = 350;
const GROUP_LIMIT = 5;

export function GlobalSearchBar() {
  const navigate = useNavigate();
  const isAdmin = authService.decodeToken()?.role === "Admin";

  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [subjects, setSubjects] = useState<SubjectSearchResult[]>([]);
  const [papers, setPapers] = useState<StudentPaper[]>([]);
  const [users, setUsers] = useState<User[]>([]);

  const containerRef = useRef<HTMLDivElement>(null);
  const latestQueryRef = useRef("");

  const runSearch = useCallback(
    async (keyword: string) => {
      setLoading(true);
      const [subjectsResult, papersResult, usersResult] = await Promise.allSettled([
        catalogService.searchSubjects({ code: keyword, pageSize: GROUP_LIMIT, all: true }),
        submissionService.searchPapers(keyword, 1, GROUP_LIMIT),
        isAdmin ? userService.getUsers(1, GROUP_LIMIT, keyword) : Promise.resolve(null),
      ]);

      // A stale response (user kept typing) must not clobber newer results.
      if (latestQueryRef.current !== keyword) return;

      setSubjects(subjectsResult.status === "fulfilled" ? subjectsResult.value.items : []);
      setPapers(papersResult.status === "fulfilled" ? papersResult.value.items : []);
      setUsers(
        usersResult.status === "fulfilled" && usersResult.value ? usersResult.value.items : []
      );
      setLoading(false);
    },
    [isAdmin]
  );

  useEffect(() => {
    const trimmed = query.trim();
    latestQueryRef.current = trimmed;

    if (!trimmed) {
      setOpen(false);
      setSubjects([]);
      setPapers([]);
      setUsers([]);
      return;
    }

    setOpen(true);
    const timer = setTimeout(() => runSearch(trimmed), DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [query, runSearch]);

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === "Escape") setOpen(false);
    };
    document.addEventListener("mousedown", handleClickOutside);
    document.addEventListener("keydown", handleEscape);
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("keydown", handleEscape);
    };
  }, []);

  const clear = () => {
    setQuery("");
    setOpen(false);
  };

  const goTo = (path: string) => {
    setOpen(false);
    setQuery("");
    navigate(path);
  };

  const hasAnyResult = subjects.length > 0 || papers.length > 0 || users.length > 0;

  return (
    <div ref={containerRef} className="relative w-full max-w-md">
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-ink-soft pointer-events-none" />
        <input
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onFocus={() => query.trim() && setOpen(true)}
          placeholder="Tìm môn thi, bài nộp, tài khoản…"
          className="w-full pl-9 pr-8 py-2 text-sm rounded-lg border border-line bg-secondary/40 text-ink placeholder:text-ink-soft/60 focus:outline-none focus:border-brand-red/40 focus:ring-1 focus:ring-brand-red/20 transition-colors"
        />
        {query && (
          <button
            type="button"
            onClick={clear}
            className="absolute right-2.5 top-1/2 -translate-y-1/2 text-ink-soft hover:text-ink"
          >
            <X className="h-4 w-4" />
          </button>
        )}
      </div>

      {open && (
        <div className="absolute left-0 right-0 mt-2 rounded-lg border border-line bg-card shadow-lg z-50 max-h-[70vh] overflow-y-auto">
          {loading ? (
            <div className="p-4 text-center text-sm text-ink-soft">Đang tìm…</div>
          ) : !hasAnyResult ? (
            <div className="p-4 text-center text-sm text-ink-soft">
              Không tìm thấy kết quả nào khớp "{query.trim()}".
            </div>
          ) : (
            <>
              {subjects.length > 0 && (
                <ResultGroup label="Môn thi">
                  {subjects.map((s) => (
                    <ResultRow
                      key={s.id}
                      title={`${s.subjectCode}${s.title ? ` — ${s.title}` : ""}`}
                      subtitle={`${s.semesterCode} · ${s.examName}`}
                      onClick={() =>
                        goTo(isAdmin ? `/admin/catalog/subjects/${s.id}` : `/catalog/subjects/${s.id}`)
                      }
                    />
                  ))}
                </ResultGroup>
              )}

              {papers.length > 0 && (
                <ResultGroup label="Bài nộp">
                  {papers.map((p) => (
                    <ResultRow
                      key={p.id}
                      title={p.studentAlias || `Paper #${p.aliasNumber}`}
                      subtitle={`${p.status} · ${p.fileCount} file`}
                      onClick={() => goTo(isAdmin ? `/admin/submissions/${p.id}` : `/submissions/${p.id}`)}
                    />
                  ))}
                </ResultGroup>
              )}

              {isAdmin && users.length > 0 && (
                <ResultGroup label="Tài khoản">
                  {users.map((u) => (
                    <ResultRow
                      key={u.id}
                      title={u.fullName || u.email}
                      subtitle={u.email}
                      onClick={() => goTo("/admin/users")}
                    />
                  ))}
                </ResultGroup>
              )}
            </>
          )}
        </div>
      )}
    </div>
  );
}

function ResultGroup({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="py-1.5 first:pt-2 last:pb-2">
      <p className="px-3 py-1 text-xs font-semibold uppercase tracking-wide text-ink-soft">
        {label}
      </p>
      {children}
    </div>
  );
}

function ResultRow({
  title,
  subtitle,
  onClick,
}: {
  title: string;
  subtitle: string;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="w-full text-left px-3 py-2 hover:bg-secondary/60 transition-colors"
    >
      <p className="text-sm font-medium text-ink truncate">{title}</p>
      <p className="text-xs text-ink-soft truncate">{subtitle}</p>
    </button>
  );
}
