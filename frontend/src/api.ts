// Thin client for the WFM API. The tenant travels in the URL and is bound to the
// Dev auth credential (step 8); the managed provider replaces the header later.
const BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:8080';

export interface Skill {
  id: string;
  name: string;
}

export interface ForecastPoint {
  start: string;
  contacts: number;
  ahtSeconds: number;
}

function authHeaders(tenantId: string): HeadersInit {
  return { Authorization: `Dev ${tenantId}` };
}

export async function seedDemo(): Promise<{ tenantId: string; skillId: string }> {
  const res = await fetch(`${BASE}/dev/seed`, { method: 'POST' });
  if (!res.ok) throw new Error('Demo seed failed');
  return res.json();
}

export async function listSkills(tenantId: string): Promise<Skill[]> {
  const res = await fetch(`${BASE}/t/${tenantId}/skills`, { headers: authHeaders(tenantId) });
  if (!res.ok) throw new Error('Listing skills failed');
  return res.json();
}

export async function triggerForecast(tenantId: string, skillId: string): Promise<void> {
  const res = await fetch(`${BASE}/t/${tenantId}/skills/${skillId}/forecast`, {
    method: 'POST',
    headers: authHeaders(tenantId),
  });
  if (res.status !== 202) throw new Error('Triggering the forecast failed');
}

export async function getForecast(tenantId: string, skillId: string): Promise<ForecastPoint[] | null> {
  const res = await fetch(`${BASE}/t/${tenantId}/skills/${skillId}/forecast`, { headers: authHeaders(tenantId) });
  if (res.status === 404) return null;
  if (!res.ok) throw new Error('Reading the forecast failed');
  return res.json();
}
