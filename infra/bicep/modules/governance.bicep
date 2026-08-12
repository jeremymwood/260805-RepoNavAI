param environmentName string
param monthlyBudgetUsd int
param alertEmail string
param postgresServerId string
param actionGroupId string

resource budget 'Microsoft.Consumption/budgets@2024-08-01' = {
  name: 'reponav-${environmentName}-monthly'
  properties: {
    amount: monthlyBudgetUsd
    category: 'Cost'
    timeGrain: 'Monthly'
    timePeriod: { startDate: '2026-08-01T00:00:00Z', endDate: '2036-08-01T00:00:00Z' }
    notifications: {
      Forecasted80: { enabled: true, operator: 'GreaterThanOrEqualTo', threshold: 80, thresholdType: 'Forecasted', contactEmails: [alertEmail] }
      Actual100: { enabled: true, operator: 'GreaterThanOrEqualTo', threshold: 100, thresholdType: 'Actual', contactEmails: [alertEmail] }
    }
  }
}

resource postgresCpuAlert 'Microsoft.Insights/metricAlerts@2026-01-01' = {
  name: 'reponav-${environmentName}-postgres-cpu'
  location: 'global'
  properties: {
    description: 'PostgreSQL CPU has remained above 85 percent.'
    severity: 2
    enabled: true
    scopes: [postgresServerId]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: { 'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria', allOf: [{ name: 'HighCpu', metricName: 'cpu_percent', metricNamespace: 'Microsoft.DBforPostgreSQL/flexibleServers', operator: 'GreaterThan', threshold: 85, timeAggregation: 'Average', criterionType: 'StaticThresholdCriterion' }] }
    actions: [{ actionGroupId: actionGroupId }]
  }
}
