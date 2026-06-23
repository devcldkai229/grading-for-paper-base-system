import { Link } from "react-router-dom";

export interface BreadcrumbItem {
  label: string;
  to?: string;
}

interface CatalogBreadcrumbProps {
  items: BreadcrumbItem[];
}

export function CatalogBreadcrumb({ items }: CatalogBreadcrumbProps) {
  return (
    <nav className="flex flex-wrap items-center gap-2 text-sm text-ink-soft mb-6">
      {items.map((item, index) => {
        const isLast = index === items.length - 1;
        return (
          <span key={`${item.label}-${index}`} className="flex items-center gap-2">
            {index > 0 && <span className="text-line">/</span>}
            {item.to && !isLast ? (
              <Link
                to={item.to}
                className="hover:text-brand-red transition-colors"
              >
                {item.label}
              </Link>
            ) : (
              <span className={isLast ? "text-ink font-medium" : undefined}>
                {item.label}
              </span>
            )}
          </span>
        );
      })}
    </nav>
  );
}
