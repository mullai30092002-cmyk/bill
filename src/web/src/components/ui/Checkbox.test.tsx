import { useState } from 'react';

import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import { Checkbox } from './Checkbox';
import { renderWithMemoryRouter } from '../../test/renderWithRouter';

const ControlledCheckbox = () => {
  const [checked, setChecked] = useState(false);

  return (
    <Checkbox
      label="Trust this device"
      helperText="Avoid this on shared billing counters."
      checked={checked}
      onChange={event => setChecked(event.target.checked)}
    />
  );
};

describe('Checkbox', () => {
  it('supports label clicks, checked binding, and helper text', async () => {
    const user = userEvent.setup();

    renderWithMemoryRouter(<ControlledCheckbox />);

    const checkbox = screen.getByRole('checkbox', { name: /trust this device/i });

    expect(checkbox).toHaveAccessibleDescription('Avoid this on shared billing counters.');
    expect(checkbox).not.toBeChecked();

    await user.click(screen.getByText(/trust this device/i));
    expect(checkbox).toBeChecked();

    await user.click(screen.getByText(/trust this device/i));
    expect(checkbox).not.toBeChecked();

    await user.click(checkbox);
    expect(checkbox).toBeChecked();
  });
});
