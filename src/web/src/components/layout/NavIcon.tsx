const paths: Record<string, string[]> = {
  home:            ['M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z', 'M9 22V12h6v10'],
  crown:           ['M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z'],
  'clipboard-check': ['M9 2h6a2 2 0 0 1 2 2v2h1a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h1V4a2 2 0 0 1 2-2z', 'M9 4h6', 'M10 12l2 2 4-4'],
  layout:          ['M3 3h7v7H3z', 'M14 3h7v7h-7z', 'M14 14h7v7h-7z', 'M3 14h7v7H3z'],
  receipt:         ['M9 5H7a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2h-2', 'M9 5a2 2 0 0 0 2 2h2a2 2 0 0 0 2-2', 'M9 5a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2', 'M9 12h6', 'M9 16h4'],
  'credit-card':   ['M1 4h22v16H1z', 'M1 10h22'],
  clock:           ['M12 22c5.523 0 10-4.477 10-10S17.523 2 12 2 2 6.477 2 12s4.477 10 10 10z', 'M12 6v6l4 2'],
  'bar-chart':     ['M18 20V10', 'M12 20V4', 'M6 20v-6'],
  'calendar-days': ['M8 2v4', 'M16 2v4', 'M3 10h18', 'M2 6a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2z', 'M8 14h.01', 'M12 14h.01', 'M16 14h.01', 'M8 18h.01', 'M12 18h.01'],
  'wallet-cards':  ['M2 5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2z', 'M2 10h20', 'M16 14h2'],
  boxes:           ['M2 2h8v8H2z', 'M14 2h8v8h-8z', 'M2 14h8v8H2z', 'M14 18h2', 'M18 14v8', 'M20 16h2'],
  hourglass:       ['M5 2h14', 'M5 22h14', 'M17 2a10 10 0 0 1-5 8.5A10 10 0 0 1 7 2', 'M7 22a10 10 0 0 1 5-8.5A10 10 0 0 1 17 22'],
  flame:           ['M8.5 14.5A2.5 2.5 0 0 0 11 17h2a2.5 2.5 0 0 0 0-5H7', 'M12 22C6.5 22 2 17.5 2 12c0-1.7.4-3.3 1.2-4.7C4.8 9.5 7 10.5 7 10.5s-1-3 2-5c0 0 1 4 5 4 0-2 .5-4 2-6 1 1 3 3.5 3 7a5 5 0 0 1-5 5'],
  package:         ['M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z', 'M3.27 6.96L12 12.01l8.73-5.05', 'M12 22.08V12'],
  users:           ['M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2', 'M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8z', 'M23 21v-2a4 4 0 0 0-3-3.87', 'M16 3.13a4 4 0 0 1 0 7.75'],
  'map-pin':       ['M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z', 'M12 13a3 3 0 1 0 0-6 3 3 0 0 0 0 6z'],
  'book-open':     ['M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z', 'M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z'],
  truck:           ['M1 3h15v13H1z', 'M16 8h4l3 3v5h-7V8z', 'M5.5 21a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5z', 'M18.5 21a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5z'],
};

export const NavIcon = ({ name }: { name: string }) => {
  const segments = paths[name];
  if (!segments) return null;

  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {segments.map((d, i) => (
        <path key={i} d={d} />
      ))}
    </svg>
  );
};

export default NavIcon;
