import { useEffect, useState } from 'react';
import { getForecast, listSkills, seedDemo, triggerForecast, type ForecastPoint, type Skill } from './api';
import { ForecastChart } from './ForecastChart';

export function App() {
  const [tenantId, setTenantId] = useState<string | null>(null);
  const [skill, setSkill] = useState<Skill | null>(null);
  const [points, setPoints] = useState<ForecastPoint[] | null>(null);
  const [status, setStatus] = useState('Starting…');

  useEffect(() => {
    void (async () => {
      try {
        const seeded = await seedDemo();
        setTenantId(seeded.tenantId);
        const skills = await listSkills(seeded.tenantId);
        setSkill(skills[0] ?? null);
        setStatus(skills.length ? 'Ready' : 'No skills');
      } catch (e) {
        setStatus(`Error: ${(e as Error).message}`);
      }
    })();
  }, []);

  async function runForecast() {
    if (!tenantId || !skill) return;
    setStatus('Forecasting…');
    setPoints(null);
    try {
      await triggerForecast(tenantId, skill.id);
      for (let attempt = 0; attempt < 60; attempt++) {
        const forecast = await getForecast(tenantId, skill.id);
        if (forecast) {
          setPoints(forecast);
          setStatus('Done');
          return;
        }
        await new Promise((resolve) => setTimeout(resolve, 500));
      }
      setStatus('Timed out waiting for the forecast');
    } catch (e) {
      setStatus(`Error: ${(e as Error).message}`);
    }
  }

  return (
    <main style={{ fontFamily: 'system-ui, sans-serif', maxWidth: 920, margin: '2rem auto', padding: '0 1rem' }}>
      <h1>WFM — Forecast</h1>
      <p>
        Skill: <strong>{skill?.name ?? '—'}</strong> · <span data-testid="status">{status}</span>
      </p>
      <button onClick={runForecast} disabled={!skill} data-testid="forecast-now">
        Forecast now
      </button>
      {points && (
        <section data-testid="forecast-chart" style={{ marginTop: 24 }}>
          <h2>Next week — forecast contacts per 15 min</h2>
          <ForecastChart points={points} />
        </section>
      )}
    </main>
  );
}
