export type HealthState = 'loading' | 'ready' | 'unavailable';

export async function checkHealth(
  endpoint: '/health/live' | '/health/ready',
  signal?: AbortSignal,
): Promise<HealthState> {
  try {
    const response = await fetch(endpoint, { cache: 'no-store', signal });
    return response.ok ? 'ready' : 'unavailable';
  } catch {
    return 'unavailable';
  }
}
