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
import {
  RiBarChartGroupedLine,
  RiFundsLine,
  RiMoneyDollarCircleLine,
  RiTimeLine,
  RiRefreshLine,
  RiCalendarCheckLine
} from 'react-icons/ri'
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
      <div className={styles.mainPanel}>
        <div className={styles.panelHeader}>
          {/* 🚀 Applied Global Title */}
          <h2 className="pos-page-title">Executive Dashboard</h2>
          <div className={styles.filterGroup}>
            <div className={styles.dateFilters}>
              <div className={styles.inputStack}>
                <label>
                  <RiCalendarCheckLine /> From
                </label>
                <input
                  type="date"
                  className="pos-input"
                  value={startDate}
                  onChange={(e) => setStartDate(e.target.value)}
                  required
                />
              </div>
              <div className={styles.inputStack}>
                <label>
                  <RiTimeLine /> To
                </label>
                <input
                  type="date"
                  className="pos-input"
                  value={endDate}
                  onChange={(e) => setEndDate(e.target.value)}
                  required
                />
              </div>
            </div>
            <button
              className={`pos-btn success ${styles.loadBtn}`}
              onClick={loadData}
              disabled={loading}
            >
              <RiRefreshLine className={loading ? styles.spin : ''} />{' '}
              {loading ? '...' : 'LOAD DATA'}
            </button>
          </div>
        </div>

        <div className={styles.panelBody}>
          {/* KPI Cards */}
          <div className={styles.kpiGrid}>
            <div className={`${styles.kpiCard} ${styles.primary}`}>
              <RiBarChartGroupedLine size={32} className={styles.kpiIcon} />
              <div className={styles.kpiContent}>
                <div className={styles.kpiTitle}>Gross Sales</div>
                <div className={styles.kpiValue}>Rs {(metrics.grossSales || 0).toFixed(2)}</div>
              </div>
            </div>
            <div className={`${styles.kpiCard} ${styles.success}`}>
              <RiFundsLine size={32} className={styles.kpiIcon} />
              <div className={styles.kpiContent}>
                <div className={styles.kpiTitle}>Net Profit</div>
                <div className={styles.kpiValue}>Rs {(metrics.netProfit || 0).toFixed(2)}</div>
              </div>
            </div>
            <div className={`${styles.kpiCard} ${styles.warning}`}>
              <RiMoneyDollarCircleLine size={32} className={styles.kpiIcon} />
              <div className={styles.kpiContent}>
                <div className={styles.kpiTitle}>Pending Credit</div>
                <div className={styles.kpiValue}>Rs {(metrics.pendingCredit || 0).toFixed(2)}</div>
              </div>
            </div>
          </div>

          {/* Chart Section */}
          <div className={styles.chartSection}>
            <div className={styles.sectionHeader}>
              <h3 className={styles.sectionTitle}>Revenue vs. Profit Analysis</h3>
            </div>
            <div className={styles.chartWrapper}>
              {chartData.length === 0 ? (
                <div className={styles.emptyChart}>No sales data found for the selected range.</div>
              ) : (
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={chartData} margin={{ top: 20, right: 30, left: 20, bottom: 5 }}>
                    <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#f1f5f9" />
                    <XAxis
                      dataKey="dateLabel"
                      axisLine={false}
                      tickLine={false}
                      tick={{ fill: '#64748b', fontSize: 12, fontWeight: 700 }}
                      dy={10}
                    />
                    <YAxis
                      tickFormatter={formatCurrency}
                      axisLine={false}
                      tickLine={false}
                      tick={{ fill: '#64748b', fontSize: 12, fontWeight: 700 }}
                    />
                    <Tooltip
                      cursor={{ fill: 'rgba(241, 245, 249, 0.6)' }}
                      contentStyle={{
                        borderRadius: '8px',
                        border: '1px solid #e2e8f0',
                        boxShadow: '0 10px 25px rgba(0,0,0,0.05)',
                        fontWeight: 700
                      }}
                      formatter={(value: any) => [`Rs ${Number(value).toFixed(2)}`, '']}
                    />
                    <Legend
                      verticalAlign="top"
                      align="right"
                      iconType="circle"
                      wrapperStyle={{ paddingBottom: '30px', fontWeight: 700, fontSize: '13px' }}
                    />
                    <Bar
                      dataKey="sales"
                      name="Gross Revenue"
                      fill="#0284c7"
                      radius={[4, 4, 0, 0]}
                      maxBarSize={50}
                    />
                    <Bar
                      dataKey="profit"
                      name="Net Profit"
                      fill="#10b981"
                      radius={[4, 4, 0, 0]}
                      maxBarSize={50}
                    />
                  </BarChart>
                </ResponsiveContainer>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
