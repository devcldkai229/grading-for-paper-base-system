interface ListPaginationProps {
  page: number;
  totalPages: number;
  totalCount: number;
  pageSize: number;
  onPageChange: (page: number) => void;
}

export function ListPagination({
  page,
  totalPages,
  totalCount,
  pageSize,
  onPageChange,
}: ListPaginationProps) {
  if (totalCount === 0) return null;

  const from = (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, totalCount);

  return (
    <div className="flex flex-col sm:flex-row items-center justify-between gap-4 mt-8">
      <p className="text-sm text-ink-soft">
        Hiển thị {from}–{to} / {totalCount}
      </p>
      {totalPages > 1 && (
        <div className="flex items-center gap-2">
          <button
            type="button"
            disabled={page <= 1}
            onClick={() => onPageChange(page - 1)}
            className="px-4 py-2 border border-line bg-card hover:bg-secondary rounded-lg text-sm text-ink disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          >
            ← Trước
          </button>
          <span className="px-3 py-2 text-sm text-ink-soft min-w-[4.5rem] text-center font-score">
            {page} / {totalPages}
          </span>
          <button
            type="button"
            disabled={page >= totalPages}
            onClick={() => onPageChange(page + 1)}
            className="px-4 py-2 border border-line bg-card hover:bg-secondary rounded-lg text-sm text-ink disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          >
            Sau →
          </button>
        </div>
      )}
    </div>
  );
}
