// src/renderer/src/pages/Reports/Dashboard.tsx
import { useState, useEffect } from 'react'
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer
} from 'recharts'
import styles from './Dashboard.module.css'

export default function Dashboard() {
  const [metrics, setMetrics] = useState({ grossSales: 0, netProfit: 0, pendingCredit: 0 })
  const [chartData, setChartData] = useState<any[]>([])
  const [loading, setLoading] = useState(false)

  const [startDate, setStartDate] = useState(() => {
    const d = new Date()
    d.setDate(1)
    return d.toISOString().split('T')[0]
  })
  const [endDate, setEndDate] = useState(() => new Date().toISOString().split('T')[0])

  const loadData = async () => {
    setLoading(true)
    try {
      // @ts-ignore
      const data = await window.api.getDashboardData(startDate, endDate)
      if (data) {
        setMetrics(data.metrics || { grossSales: 0, netProfit: 0, pendingCredit: 0 })
        setChartData(data.chartData || [])
      }
    } catch (error) {
      console.error('Failed to load dashboard data', error)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    loadData()
  }, [startDate, endDate])

  const formatCurrency = (value: number) => `Rs ${value.toLocaleString()}`

  return (
    <div className={styles.container}>
      <div className={styles.controlHeader}>
        <h2 className={styles.pageTitle}>EXECUTIVE DASHBOARD</h2>
        <div className={styles.filterGroup}>
          <div className={styles.dateFilters}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
              <label
                style={{
                  fontSize: '11px',
                  fontWeight: 800,
                  color: 'var(--text-muted)',
                  textTransform: 'uppercase'
                }}
              >
                From
              </label>
              <input
                type="date"
                className="pos-input"
                style={{ padding: '8px' }}
                value={startDate}
                onChange={(e) => setStartDate(e.target.value)}
                required
              />
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
              <label
                style={{
                  fontSize: '11px',
                  fontWeight: 800,
                  color: 'var(--text-muted)',
                  textTransform: 'uppercase'
                }}
              >
                To
              </label>
              <input
                type="date"
                className="pos-input"
                style={{ padding: '8px' }}
                value={endDate}
                onChange={(e) => setEndDate(e.target.value)}
                required
              />
            </div>
          </div>
          <button
            className="pos-btn success"
            style={{
              alignSelf: 'flex-end',
              minHeight: '44px',
              padding: '10px 20px',
              fontSize: '14px'
            }}
            onClick={loadData}
            disabled={loading}
          >
            {loading ? '⏳' : '🔄 LOAD'}
          </button>
        </div>
      </div>

      <div className={styles.kpiGrid}>
        <div className={`${styles.kpiCard} ${styles.primary}`}>
          <div className={styles.kpiTitle}>Gross Sales (Selected Period)</div>
          <div className={styles.kpiValue}>Rs {(metrics.grossSales || 0).toFixed(2)}</div>
        </div>
        <div className={`${styles.kpiCard} ${styles.success}`}>
          <div className={styles.kpiTitle}>Net Profit (Selected Period)</div>
          <div className={styles.kpiValue}>Rs {(metrics.netProfit || 0).toFixed(2)}</div>
        </div>
        <div className={`${styles.kpiCard} ${styles.warning}`}>
          <div className={styles.kpiTitle}>Pending Credit Owed (All Time)</div>
          <div className={styles.kpiValue}>Rs {(metrics.pendingCredit || 0).toFixed(2)}</div>
        </div>
      </div>

      <div className={styles.chartCard}>
        <div className={styles.cardHeader}>
          <div className={styles.cardTitle}>Revenue vs. Profit Analysis</div>
        </div>
        <div className={styles.chartWrapper}>
          {chartData.length === 0 ? (
            <div className={styles.emptyChart}>
              No sales data found for the selected date range.
            </div>
          ) : (
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={chartData} margin={{ top: 10, right: 10, left: 20, bottom: 5 }}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e2e8f0" />
                <XAxis
                  dataKey="dateLabel"
                  axisLine={false}
                  tickLine={false}
                  tick={{ fill: '#64748b', fontSize: 12, fontWeight: 600 }}
                  dy={10}
                />
                <YAxis
                  tickFormatter={formatCurrency}
                  axisLine={false}
                  tickLine={false}
                  tick={{ fill: '#64748b', fontSize: 12, fontWeight: 600 }}
                />
                <Tooltip
                  cursor={{ fill: 'rgba(241, 245, 249, 0.5)' }}
                  contentStyle={{
                    borderRadius: '8px',
                    border: '1px solid #e2e8f0',
                    boxShadow: '0 10px 25px rgba(0,0,0,0.1)'
                  }}
                  formatter={(value: any) => [`Rs ${Number(value).toFixed(2)}`, '']}
                />
                <Legend iconType="circle" wrapperStyle={{ paddingTop: '20px', fontWeight: 700 }} />
                <Bar
                  dataKey="sales"
                  name="Gross Revenue"
                  fill="#3b82f6"
                  radius={[4, 4, 0, 0]}
                  maxBarSize={60}
                />
                <Bar
                  dataKey="profit"
                  name="Net Profit"
                  fill="#10b981"
                  radius={[4, 4, 0, 0]}
                  maxBarSize={60}
                />
              </BarChart>
            </ResponsiveContainer>
          )}
        </div>
      </div>
    </div>
  )
}
