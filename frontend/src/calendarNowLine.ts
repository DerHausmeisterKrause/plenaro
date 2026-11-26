import { localDate } from './tasksApi';

export type CalendarNowLine = { hour:number; dayIndex:number; topPercent:number };

/** Same rules as WeekView.xaml: one marker for the local current time,
 * visible only inside the displayed weekday and 06:00–18:00 grid. */
export function calendarNowLine(now:Date,days:Date[]):CalendarNowLine|null {
  const hour=now.getHours();
  if(hour<6||hour>=18)return null;
  const dayIndex=days.findIndex(day=>localDate(day)===localDate(now));
  if(dayIndex<0)return null;
  return {hour,dayIndex,topPercent:
    (now.getMinutes()+now.getSeconds()/60)/60*100};
}
