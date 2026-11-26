/** ISO 8601 week number: Monday starts the week and week 1 has January 4.
 * UTC arithmetic avoids daylight-saving and year-transition errors. */
export function isoWeekNumber(value:Date):number {
  const thursday=new Date(Date.UTC(value.getFullYear(),value.getMonth(),value.getDate()));
  const day=thursday.getUTCDay()||7;
  thursday.setUTCDate(thursday.getUTCDate()+4-day);
  const first=new Date(Date.UTC(thursday.getUTCFullYear(),0,1));
  return Math.ceil(((thursday.getTime()-first.getTime())/86400000+1)/7);
}
