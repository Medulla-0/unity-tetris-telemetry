let scoreApmChartInstance = null;
let lineTypesChartInstance = null;
let blocksChartInstance = null;
let actionsChartInstance = null;
let lastDataSignature = '';

Chart.defaults.color = '#8a919e';
Chart.defaults.font.family = "'Inter', sans-serif";
Chart.defaults.font.size = 11;

function formatNumber(n) {
  return Number(n || 0).toLocaleString('tr-TR');
}

async function fetchDashboardData() {
  try {
    const res = await fetch('/api/stats');
    const json = await res.json();
    if (json.ok) {
      const signature = JSON.stringify(json.data);
      if (signature === lastDataSignature) {
        return;
      }
      lastDataSignature = signature;
      renderDashboard(json.data);
    }
  } catch (err) {
    console.error('Veri cekme hatasi:', err);
  }
}

function renderDashboard(data) {
  const s = data.summary || {};

  document.getElementById('kpiSessions').textContent = formatNumber(s.total_sessions);
  document.getElementById('kpiAvgDuration').textContent = `${s.avg_duration || 0} sn`;
  document.getElementById('kpiHighScore').textContent = formatNumber(s.high_score);
  document.getElementById('kpiAvgScore').textContent = formatNumber(s.avg_score);
  document.getElementById('kpiBestCombo').textContent = `x${s.best_combo || 0}`;
  document.getElementById('kpiMaxLevel').textContent = `Lv.${s.highest_level || 1}`;
  document.getElementById('kpiTotalLines').textContent = formatNumber(s.total_lines);

  const totalLineEvents = (s.total_singles || 0) + (s.total_doubles || 0) + (s.total_triples || 0) + (s.total_tetrises || 0);
  const tetrisRate = totalLineEvents > 0 ? Math.round(((s.total_tetrises || 0) / totalLineEvents) * 100) : 0;
  document.getElementById('kpiTetrisRate').textContent = `%${tetrisRate}`;

  document.getElementById('kpiAvgApm').textContent = s.avg_apm || 0;
  document.getElementById('kpiTotalBlocks').textContent = formatNumber(s.total_blocks);

  const totalInputs = (s.sum_moves || 0) + (s.sum_rotations || 0) + (s.sum_hard_drops || 0) + (s.sum_soft_drops || 0) + (s.sum_holds || 0);
  document.getElementById('kpiTotalInputs').textContent = formatNumber(totalInputs);

  renderRecentSessions(data.recentSessions || []);
  renderLeaderboard(data.leaderboard || []);

  renderScoreApmChart(data.recentSessions || []);
  renderLineTypesChart(s);
  renderBlocksChart(s);
  renderActionsChart(s);

  const activeGame = (data.recentSessions || []).find(r => r.status === 'active');
  const statusTextEl = document.getElementById('liveStatusText');
  const statusDotEl = document.getElementById('statusDot');
  if (statusTextEl && statusDotEl) {
    if (activeGame) {
      statusTextEl.textContent = `Aktif Oyun: ${activeGame.session_id}`;
      statusDotEl.classList.add('active');
    } else {
      statusTextEl.textContent = 'Unity bekleniyor';
      statusDotEl.classList.remove('active');
    }
  }
}

function renderRecentSessions(rows) {
  const tbody = document.getElementById('recentSessionsBody');
  if (!rows.length) {
    tbody.innerHTML = '<tr><td colspan="9" style="text-align:center;color:#8a919e;padding:1.25rem;">Henüz kayıtlı oyun yok. Unity\'de oyunu başlattığınızda Oyun ID otomatik eklenecek.</td></tr>';
    return;
  }

  tbody.innerHTML = rows.map(r => {
    const isLive = r.status === 'active';
    const statusBadge = isLive
      ? '<span class="status-badge status-active">Oynanıyor</span>'
      : '<span class="status-badge status-completed">Bitti</span>';

    return `
      <tr>
        <td>${statusBadge}</td>
        <td class="mono">${r.session_id}</td>
        <td class="mono">${formatNumber(r.score)}</td>
        <td class="mono">Lv.${r.max_level}</td>
        <td class="mono">${r.singles} / ${r.doubles} / ${r.triples} / ${r.tetrises}</td>
        <td class="mono">${r.blocks_spawned}</td>
        <td class="mono">${r.total_moves} / ${r.total_rotations} / ${r.hard_drops} / ${r.hold_count || 0}</td>
        <td class="mono">${r.duration_seconds} sn</td>
        <td style="color:#8a919e;">${r.updated_at || ''}</td>
      </tr>
    `;
  }).join('');
}

function renderLeaderboard(rows) {
  const tbody = document.getElementById('leaderboardBody');
  if (!rows.length) {
    tbody.innerHTML = '<tr><td colspan="7" style="text-align:center;color:#8a919e;padding:1.25rem;">Kayıt yok</td></tr>';
    return;
  }

  tbody.innerHTML = rows.map((r, idx) => `
    <tr>
      <td class="mono">${idx + 1}</td>
      <td class="mono">${r.session_id}</td>
      <td class="mono">${formatNumber(r.score)}</td>
      <td class="mono">Lv.${r.max_level}</td>
      <td class="mono">x${r.max_combo}</td>
      <td class="mono">${r.lines_cleared}</td>
      <td class="mono">${r.apm}</td>
    </tr>
  `).join('');
}

function renderScoreApmChart(recentSessions) {
  const ordered = [...recentSessions].reverse();
  const labels = ordered.map(r => r.session_id);
  const scores = ordered.map(r => r.score);
  const apms = ordered.map(r => r.apm);

  if (scoreApmChartInstance) {
    scoreApmChartInstance.data.labels = labels;
    scoreApmChartInstance.data.datasets[0].data = scores;
    scoreApmChartInstance.data.datasets[1].data = apms;
    scoreApmChartInstance.update('none');
    return;
  }

  const ctx = document.getElementById('scoreApmChart').getContext('2d');
  scoreApmChartInstance = new Chart(ctx, {
    type: 'bar',
    data: {
      labels,
      datasets: [
        {
          label: 'Skor',
          data: scores,
          backgroundColor: '#3b82f6',
          borderRadius: 3,
          yAxisID: 'y'
        },
        {
          label: 'APM',
          data: apms,
          type: 'line',
          borderColor: '#9ca3af',
          borderWidth: 2,
          pointRadius: 3,
          tension: 0.2,
          yAxisID: 'y1'
        }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      animation: false,
      interaction: { mode: 'index', intersect: false },
      scales: {
        y: {
          type: 'linear',
          position: 'left',
          grid: { color: '#262a31' }
        },
        y1: {
          type: 'linear',
          position: 'right',
          grid: { drawOnChartArea: false }
        }
      }
    }
  });
}

function renderLineTypesChart(s) {
  const newData = [s.total_singles || 0, s.total_doubles || 0, s.total_triples || 0, s.total_tetrises || 0];

  if (lineTypesChartInstance) {
    lineTypesChartInstance.data.datasets[0].data = newData;
    lineTypesChartInstance.update('none');
    return;
  }

  const ctx = document.getElementById('lineTypesChart').getContext('2d');
  lineTypesChartInstance = new Chart(ctx, {
    type: 'bar',
    data: {
      labels: ['1 Satır (Single)', '2 Satır (Double)', '3 Satır (Triple)', '4 Satır (Tetris)'],
      datasets: [{
        label: 'Adet',
        data: newData,
        backgroundColor: ['#4b5563', '#3b82f6', '#10b981', '#f59e0b'],
        borderRadius: 3
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      animation: false,
      plugins: { legend: { display: false } },
      scales: {
        y: { grid: { color: '#262a31' } }
      }
    }
  });
}

function renderBlocksChart(s) {
  const newData = [
    s.sum_i || 0, s.sum_o || 0, s.sum_t || 0,
    s.sum_l || 0, s.sum_j || 0, s.sum_s || 0, s.sum_z || 0
  ];

  if (blocksChartInstance) {
    blocksChartInstance.data.datasets[0].data = newData;
    blocksChartInstance.update('none');
    return;
  }

  const ctx = document.getElementById('blocksChart').getContext('2d');
  blocksChartInstance = new Chart(ctx, {
    type: 'bar',
    data: {
      labels: ['I', 'O', 'T', 'L', 'J', 'S', 'Z'],
      datasets: [{
        label: 'Blok Sayısı',
        data: newData,
        backgroundColor: '#3b82f6',
        borderRadius: 3
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      animation: false,
      plugins: { legend: { display: false } },
      scales: {
        y: { grid: { color: '#262a31' } }
      }
    }
  });
}

function renderActionsChart(s) {
  const newData = [
    s.sum_moves || 0,
    s.sum_rotations || 0,
    s.sum_soft_drops || 0,
    s.sum_hard_drops || 0,
    s.sum_holds || 0
  ];

  if (actionsChartInstance) {
    actionsChartInstance.data.datasets[0].data = newData;
    actionsChartInstance.update('none');
    return;
  }

  const ctx = document.getElementById('actionsChart').getContext('2d');
  actionsChartInstance = new Chart(ctx, {
    type: 'bar',
    data: {
      labels: ['Sol/Sağ Hamle', 'Döndürme', 'Soft Drop', 'Hard Drop', 'Blok Saklama (Hold)'],
      datasets: [{
        label: 'Adet',
        data: newData,
        backgroundColor: '#64748b',
        borderRadius: 3
      }]
    },
    options: {
      indexAxis: 'y',
      responsive: true,
      maintainAspectRatio: false,
      animation: false,
      plugins: { legend: { display: false } },
      scales: {
        x: { grid: { color: '#262a31' } }
      }
    }
  });
}

document.getElementById('btnReset').addEventListener('click', async () => {
  if (!confirm('Tüm oyun kayıtları silinsin mi?')) return;
  await fetch('/api/reset', { method: 'DELETE' });
  lastDataSignature = '';
  fetchDashboardData();
});

fetchDashboardData();
setInterval(fetchDashboardData, 2000);
