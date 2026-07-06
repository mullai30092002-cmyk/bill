import { AdminLayout } from '../../components/layout';
import { Button, Card, Input, ResponsiveDataList, Select, StatusBadge, SummaryCard } from '../../components/ui';
import type { ShellNavItem } from '../../components/layout/navigation';

export interface AdminUsersPreviewPageProps {
  navItems?: ShellNavItem[];
  restaurantName?: string;
  branchName?: string;
  operatorLabel?: string;
}

const adminRows = [
  {
    id: 'u1',
    name: 'Mohan Kumar',
    role: 'RestaurantOwner',
    branch: 'Main branch',
    status: 'Active',
    lastSignIn: 'Today, 08:12',
  },
  {
    id: 'u2',
    name: 'Priya Devi',
    role: 'Cashier',
    branch: 'Main branch',
    status: 'Active',
    lastSignIn: 'Today, 10:26',
  },
  {
    id: 'u3',
    name: 'Arun',
    role: 'Waiter',
    branch: 'Main branch',
    status: 'Pending',
    lastSignIn: 'Invite sent',
  },
  {
    id: 'u4',
    name: 'Karthik',
    role: 'InventoryUser',
    branch: 'Warehouse',
    status: 'Inactive',
    lastSignIn: '2 days ago',
  },
];

export const AdminUsersPreviewPage = ({
  navItems,
  restaurantName,
  branchName,
  operatorLabel,
}: AdminUsersPreviewPageProps) => (
  <AdminLayout
    title="Admin users preview"
    description="Configuration screens need clear breadcrumbs, forms, and tables without overwhelming staff-focused layouts."
    breadcrumbs={['Dashboard', 'Admin preview', 'Users']}
    operatorLabel={operatorLabel ?? 'Admin preview'}
    navItems={navItems}
    restaurantName={restaurantName}
    branchName={branchName}
  >
    <div className="preview-sequence">
      <div className="summary-grid">
        <SummaryCard label="Users" value="4" detail="Preview list with mixed roles and states." tone="admin" />
        <SummaryCard
          label="Roles"
          value="8"
          detail="Role selection should remain explicit and branch-aware."
          tone="accent"
        />
        <SummaryCard
          label="Pending invites"
          value="1"
          detail="Need a clear visual state before activation."
          tone="orders"
        />
        <SummaryCard
          label="Inactive"
          value="1"
          detail="Inactive users must remain auditable rather than deleted."
          tone="inventory"
        />
      </div>

      <div className="preview-split preview-split--admin">
        <Card title="User management shell" description="Filters and forms sit next to the data list." tone="admin">
          <div className="admin-controls">
            <Input label="Search users" placeholder="Name, mobile, or role" helperText="Typing should stay short." />
            <Select label="Role" defaultValue="Cashier" helperText="Role changes should be obvious and auditable.">
              <option>RestaurantOwner</option>
              <option>Admin</option>
              <option>Cashier</option>
              <option>Waiter</option>
              <option>KitchenUser</option>
              <option>InventoryUser</option>
              <option>AccountsUser</option>
            </Select>
            <div className="admin-control-row">
              <Button>Invite user</Button>
              <Button variant="secondary">Deactivate</Button>
            </div>
          </div>
        </Card>

        <Card title="Responsive user list" description="Tables on desktop, cards on mobile." tone="accent">
          <ResponsiveDataList
            rows={adminRows}
            columns={[
              { key: 'name', label: 'User' },
              { key: 'role', label: 'Role' },
              { key: 'branch', label: 'Branch' },
              {
                key: 'status',
                label: 'Status',
                render: row => <StatusBadge status={row.status} />,
              },
              { key: 'lastSignIn', label: 'Last sign-in', align: 'right' },
            ]}
            mobileTitle={row => row.name}
            mobileDescription={row => row.role}
            emptyTitle="No admin users"
            emptyDescription="Add a user preview row to verify the responsive list."
          />
        </Card>
      </div>
    </div>
  </AdminLayout>
);

export default AdminUsersPreviewPage;
