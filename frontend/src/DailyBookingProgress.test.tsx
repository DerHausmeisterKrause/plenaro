import {afterEach,describe,expect,it} from 'vitest';
import {cleanup,render,screen} from '@testing-library/react';
import {DailyBookingProgress} from './DailyBookingProgress';

afterEach(cleanup);

describe('Daily Znuny booking progress',()=>{
  it('shows booked and remaining agent hours with accessible progress value',()=>{
    render(<DailyBookingProgress quota={{
      day:'2026-11-19',limitMinutes:1440,
      reservedMinutes:510,remainingMinutes:930
    }}/>);
    expect(screen.getByText('08:30 h / 24:00 h')).toBeTruthy();
    expect(screen.getByText('15:30 h noch frei')).toBeTruthy();
    const progress=screen.getByRole('progressbar');
    expect(progress.getAttribute('aria-valuenow')).toBe('510');
    expect(progress.getAttribute('aria-valuemax')).toBe('1440');
    expect(screen.getByText(/noch ungeklärter Übertragungen/)).toBeTruthy();
  });

  it('renders exactly 24 hours full and clamps over-limit historical data',()=>{
    render(<DailyBookingProgress compact quota={{
      day:'2026-11-19',limitMinutes:1440,
      reservedMinutes:1455,remainingMinutes:0
    }}/>);
    expect(screen.getByText('24:15 h / 24:00 h')).toBeTruthy();
    const progress=screen.getByRole('progressbar');
    expect(progress.getAttribute('aria-valuenow')).toBe('1440');
    expect(screen.getByText('00:00 h noch frei')).toBeTruthy();
    expect(document.querySelector('.znuny-daily-progress-fill')?.getAttribute('style'))
      .toContain('width: 100%');
  });
});
