import { useState } from 'react';
import { OrderManagementLayout } from '../../components/layout';
import { ActionTile, Badge, Button, Card, StatusBadge, SummaryCard } from '../../components/ui';
import type { ShellNavItem } from '../../components/layout/navigation';

export interface OrderWorkspacePreviewPageProps {
  navItems?: ShellNavItem[];
  restaurantName?: string;
  branchName?: string;
  operatorLabel?: string;
}

const categories = [
  { id: 'starters', title: 'Starters', hint: 'Samosa, soups, fries' },
  { id: 'tandoor', title: 'Tandoor', hint: 'Roti, naan, kebab' },
  { id: 'curries', title: 'Curries', hint: 'Chicken, mutton, veg' },
  { id: 'rice', title: 'Rice & biryani', hint: 'Fried rice, biryani, meals' },
  { id: 'drinks', title: 'Drinks', hint: 'Tea, coffee, cool drinks' },
  { id: 'desserts', title: 'Desserts', hint: 'Ice cream, sweet finish' },
];

const menuItemsByCategory: Record<string, Array<{ title: string; price: string; note: string }>> = {
  starters: [
    { title: 'Vegetable Samosa', price: '₹28', note: '2 pieces' },
    { title: 'Pepper Soup', price: '₹72', note: 'Served hot' },
    { title: 'Crispy Fries', price: '₹65', note: 'Shared snack' },
  ],
  tandoor: [
    { title: 'Butter Naan', price: '₹22', note: 'Freshly baked' },
    { title: 'Chicken Kebab', price: '₹145', note: 'Grill item' },
    { title: 'Paneer Tikka', price: '₹135', note: 'Vegetarian' },
  ],
  curries: [
    { title: 'Chicken Curry', price: '₹168', note: 'House special' },
    { title: 'Mutton Chettinad', price: '₹245', note: 'Spicy' },
    { title: 'Veg Kurma', price: '₹124', note: 'Mild' },
  ],
  rice: [
    { title: 'Chicken Biryani', price: '₹198', note: 'Signature plate' },
    { title: 'Veg Fried Rice', price: '₹138', note: 'Fast prep' },
    { title: 'Curd Rice', price: '₹84', note: 'Comfort meal' },
  ],
  drinks: [
    { title: 'Masala Tea', price: '₹18', note: 'Hot cup' },
    { title: 'Fresh Lime', price: '₹42', note: 'Cold drink' },
    { title: 'Filter Coffee', price: '₹24', note: 'Strong' },
  ],
  desserts: [
    { title: 'Ice Cream Scoop', price: '₹55', note: 'Vanilla / chocolate' },
    { title: 'Gulab Jamun', price: '₹48', note: '2 pieces' },
    { title: 'Falooda', price: '₹92', note: 'Sweet finish' },
  ],
};

const currentOrder = [
  { id: '1', item: 'Chicken Biryani', qty: '2', rate: '₹198', amount: '₹396' },
  { id: '2', item: 'Butter Naan', qty: '4', rate: '₹22', amount: '₹88' },
  { id: '3', item: 'Masala Tea', qty: '2', rate: '₹18', amount: '₹36' },
];

const ticketSteps = ['Queued', 'Accepted', 'Preparing', 'Ready'];

export const OrderWorkspacePreviewPage = ({
  navItems,
  restaurantName,
  branchName,
  operatorLabel,
}: OrderWorkspacePreviewPageProps) => {
  const [orderType, setOrderType] = useState<'Eat-in' | 'Parcel'>('Eat-in');
  const [selectedCategory, setSelectedCategory] = useState('rice');

  const visibleItems = menuItemsByCategory[selectedCategory] ?? [];

  return (
    <OrderManagementLayout
      title="Order workspace preview"
      description="Touch-first ordering for eat-in and parcel flows, with a persistent ticket panel and large action targets."
      breadcrumbs={['Dashboard', 'Orders preview']}
      operatorLabel={operatorLabel ?? 'Order desk preview'}
      navItems={navItems}
      restaurantName={restaurantName}
      branchName={branchName}
    >
      <div className="preview-shell preview-shell--orders">
        <section className="preview-main">
          <Card title="Order type" description="Switch between eat-in and parcel/takeaway." tone="orders">
            <div className="toggle-row">
              {(['Eat-in', 'Parcel'] as const).map(type => (
                <button
                  key={type}
                  type="button"
                  className={['segment-button', orderType === type && 'segment-button--active']
                    .filter(Boolean)
                    .join(' ')}
                  onClick={() => setOrderType(type)}
                >
                  {type}
                </button>
              ))}
            </div>
            <div className="mini-status-row">
              <Badge tone="primary" label={orderType === 'Eat-in' ? 'Table 12' : 'Token P-204'} />
              <Badge tone="accent" label="Kitchen linked" />
              <Badge tone="info" label="No price edits" />
            </div>
          </Card>

          <Card title="Category tiles" description="Large touch targets for fast item browsing." tone="orders">
            <div className="preview-tile-grid preview-tile-grid--categories">
              {categories.map(category => (
                <ActionTile
                  key={category.id}
                  title={category.title}
                  description={category.hint}
                  tone="orders"
                  selected={selectedCategory === category.id}
                  onClick={() => setSelectedCategory(category.id)}
                />
              ))}
            </div>
          </Card>

          <Card title="Item tiles" description="Choose items quickly without dense tables." tone="orders">
            <div className="preview-tile-grid preview-tile-grid--items">
              {visibleItems.map(item => (
                <ActionTile
                  key={item.title}
                  title={item.title}
                  description={item.note}
                  badge={item.price}
                  tone="orders"
                />
              ))}
            </div>
          </Card>
        </section>

        <aside className="preview-aside">
          <Card title="Current order" description="Persistent panel for the running ticket." tone="accent">
            <div className="order-summary">
              <SummaryCard label="Items" value="8" detail="Total lines in the open ticket." tone="orders" />
              <SummaryCard label="Net total" value="₹520" detail="Preview total only." tone="accent" />
            </div>
            <div className="order-line-list">
              {currentOrder.map(line => (
                <div key={line.id} className="order-line">
                  <div className="order-line__main">
                    <strong>{line.item}</strong>
                    <span>
                      {line.qty} x {line.rate}
                    </span>
                  </div>
                  <strong>{line.amount}</strong>
                </div>
              ))}
            </div>
            <div className="order-action-row">
              <Button fullWidth>Send to kitchen</Button>
              <Button variant="secondary" fullWidth>
                Add item
              </Button>
              <Button variant="secondary" fullWidth>
                Bill later
              </Button>
            </div>
          </Card>

          <Card title="Kitchen ticket preview" description="Readable status ladder for the prep counter." tone="accent">
            <div className="ticket-preview">
              <div className="ticket-preview__meta">
                <Badge tone="warning" label={orderType === 'Eat-in' ? 'Eat-in' : 'Parcel'} />
                <StatusBadge status="Preparing" />
              </div>
              <div className="ticket-preview__number">{orderType === 'Eat-in' ? 'Table 12' : 'Token P-204'}</div>
              <div className="ticket-preview__steps">
                {ticketSteps.map(step => (
                  <div key={step} className={['ticket-step', step === 'Preparing' && 'ticket-step--active'].filter(Boolean).join(' ')}>
                    {step}
                  </div>
                ))}
              </div>
            </div>
          </Card>

          <Card title="Fast actions" description="Placeholders for quick staff moves." tone="orders">
            <div className="preview-checks">
              <Button variant="secondary" fullWidth>
                Hold ticket
              </Button>
              <Button variant="secondary" fullWidth>
                Print token
              </Button>
              <Button variant="secondary" fullWidth>
                Open bill preview
              </Button>
            </div>
          </Card>
        </aside>
      </div>
    </OrderManagementLayout>
  );
};

export default OrderWorkspacePreviewPage;
