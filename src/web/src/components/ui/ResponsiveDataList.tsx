import type { ReactNode } from 'react';
import { EmptyState } from './EmptyState';

export interface ResponsiveDataColumn<Row extends { id: string }> {
  key: keyof Row & string;
  label: string;
  render?: (row: Row) => ReactNode;
  hideOnMobile?: boolean;
  align?: 'left' | 'right';
}

export interface ResponsiveDataListProps<Row extends { id: string }> {
  columns: Array<ResponsiveDataColumn<Row>>;
  rows: Row[];
  mobileTitle: (row: Row) => ReactNode;
  mobileDescription?: (row: Row) => ReactNode;
  emptyTitle: string;
  emptyDescription: string;
  className?: string;
}

export const ResponsiveDataList = <Row extends { id: string }>({
  columns,
  rows,
  mobileTitle,
  mobileDescription,
  emptyTitle,
  emptyDescription,
  className,
}: ResponsiveDataListProps<Row>) => {
  if (rows.length === 0) {
    return <EmptyState title={emptyTitle} description={emptyDescription} tone="default" />;
  }

  return (
    <div className={['responsive-data-list', className].filter(Boolean).join(' ')}>
      <div className="responsive-data-list__desktop">
        <table className="responsive-data-list__table">
          <thead>
            <tr>
              {columns.map(column => (
                <th key={column.key} className={column.align === 'right' ? 'is-right' : undefined}>
                  {column.label}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map(row => (
              <tr key={row.id}>
                {columns.map(column => (
                  <td
                    key={column.key}
                    className={column.align === 'right' ? 'is-right' : undefined}
                  >
                    {column.render ? column.render(row) : String(row[column.key] ?? '')}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="responsive-data-list__mobile">
        {rows.map(row => (
          <article key={row.id} className="responsive-data-list__card">
            <div className="responsive-data-list__card-title">{mobileTitle(row)}</div>
            {mobileDescription ? (
              <div className="responsive-data-list__card-description">{mobileDescription(row)}</div>
            ) : null}
            <dl className="responsive-data-list__card-fields">
              {columns
                .filter(column => !column.hideOnMobile)
                .map(column => (
                  <div key={column.key} className="responsive-data-list__card-field">
                    <dt>{column.label}</dt>
                    <dd>{column.render ? column.render(row) : String(row[column.key] ?? '')}</dd>
                  </div>
                ))}
            </dl>
          </article>
        ))}
      </div>
    </div>
  );
};

export default ResponsiveDataList;
