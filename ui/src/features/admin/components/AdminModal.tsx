import type { ReactNode } from "react";

interface AdminModalProps {
  open: boolean;
  title: string;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
}

export function AdminModal({ open, title, onClose, children, footer }: AdminModalProps) {
  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <button
        type="button"
        className="absolute inset-0 bg-ink/40"
        aria-label="Đóng"
        onClick={onClose}
      />
      <div className="relative w-full max-w-lg bg-card border border-line rounded-xl shadow-lg">
        <div className="flex items-center justify-between px-5 py-4 border-b border-line">
          <h2 className="font-display text-lg font-semibold text-ink">{title}</h2>
          <button
            type="button"
            onClick={onClose}
            className="text-ink-soft hover:text-ink text-xl leading-none"
          >
            ×
          </button>
        </div>
        <div className="px-5 py-4 space-y-4">{children}</div>
        {footer && (
          <div className="px-5 py-4 border-t border-line flex justify-end gap-2">{footer}</div>
        )}
      </div>
    </div>
  );
}

const fieldClass =
  "w-full px-3 py-2 bg-card border border-line rounded-lg text-sm text-ink focus:border-primary outline-none";

export function AdminField({
  label,
  children,
}: {
  label: string;
  children: ReactNode;
}) {
  return (
    <label className="block space-y-1.5">
      <span className="text-sm font-medium text-ink-soft">{label}</span>
      {children}
    </label>
  );
}

export function AdminTextInput(props: React.InputHTMLAttributes<HTMLInputElement>) {
  return <input {...props} className={fieldClass} />;
}

export function AdminTextArea(props: React.TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea {...props} className={`${fieldClass} min-h-[80px]`} />;
}

export function AdminSelect(props: React.SelectHTMLAttributes<HTMLSelectElement>) {
  return <select {...props} className={fieldClass} />;
}

export function AdminPrimaryButton({
  children,
  ...props
}: React.ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      type="button"
      {...props}
      className="px-4 py-2 rounded-lg text-sm font-medium bg-primary text-primary-foreground hover:opacity-90 disabled:opacity-50"
    >
      {children}
    </button>
  );
}

export function AdminSecondaryButton({
  children,
  ...props
}: React.ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      type="button"
      {...props}
      className="px-4 py-2 rounded-lg text-sm font-medium border border-line text-ink hover:bg-secondary disabled:opacity-50"
    >
      {children}
    </button>
  );
}
