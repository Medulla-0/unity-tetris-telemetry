const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const { DatabaseSync } = require('node:sqlite');

const PORT = process.env.PORT || 8080;
const DB_PATH = path.join(__dirname, 'telemetry.db');
const PUBLIC_DIR = path.join(__dirname, 'public');

const db = new DatabaseSync(DB_PATH);

db.exec(`
  PRAGMA journal_mode = WAL;
  CREATE TABLE IF NOT EXISTS sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id TEXT UNIQUE NOT NULL,
    player_name TEXT DEFAULT 'Oyuncu',
    device_id TEXT DEFAULT 'local',
    platform TEXT DEFAULT 'UnityEditor',
    score INTEGER DEFAULT 0,
    max_level INTEGER DEFAULT 1,
    max_combo INTEGER DEFAULT 0,
    lines_cleared INTEGER DEFAULT 0,
    singles INTEGER DEFAULT 0,
    doubles INTEGER DEFAULT 0,
    triples INTEGER DEFAULT 0,
    tetrises INTEGER DEFAULT 0,
    blocks_spawned INTEGER DEFAULT 0,
    block_i INTEGER DEFAULT 0,
    block_o INTEGER DEFAULT 0,
    block_t INTEGER DEFAULT 0,
    block_l INTEGER DEFAULT 0,
    block_j INTEGER DEFAULT 0,
    block_s INTEGER DEFAULT 0,
    block_z INTEGER DEFAULT 0,
    total_moves INTEGER DEFAULT 0,
    total_rotations INTEGER DEFAULT 0,
    hard_drops INTEGER DEFAULT 0,
    soft_drops INTEGER DEFAULT 0,
    hold_count INTEGER DEFAULT 0,
    duration_seconds REAL DEFAULT 0,
    apm REAL DEFAULT 0,
    status TEXT DEFAULT 'completed',
    created_at TEXT DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT DEFAULT (datetime('now', 'localtime'))
  );
`);

try {
  db.exec(`ALTER TABLE sessions ADD COLUMN hold_count INTEGER DEFAULT 0;`);
} catch (_) {}

const upsertStmt = db.prepare(`
  INSERT INTO sessions (
    session_id, player_name, device_id, platform,
    score, max_level, max_combo,
    lines_cleared, singles, doubles, triples, tetrises,
    blocks_spawned, block_i, block_o, block_t, block_l, block_j, block_s, block_z,
    total_moves, total_rotations, hard_drops, soft_drops, hold_count,
    duration_seconds, apm, status, updated_at
  ) VALUES (
    $session_id, $player_name, $device_id, $platform,
    $score, $max_level, $max_combo,
    $lines_cleared, $singles, $doubles, $triples, $tetrises,
    $blocks_spawned, $block_i, $block_o, $block_t, $block_l, $block_j, $block_s, $block_z,
    $total_moves, $total_rotations, $hard_drops, $soft_drops, $hold_count,
    $duration_seconds, $apm, $status, datetime('now', 'localtime')
  )
  ON CONFLICT(session_id) DO UPDATE SET
    player_name = excluded.player_name,
    platform = excluded.platform,
    score = excluded.score,
    max_level = excluded.max_level,
    max_combo = MAX(sessions.max_combo, excluded.max_combo),
    lines_cleared = excluded.lines_cleared,
    singles = excluded.singles,
    doubles = excluded.doubles,
    triples = excluded.triples,
    tetrises = excluded.tetrises,
    blocks_spawned = excluded.blocks_spawned,
    block_i = excluded.block_i,
    block_o = excluded.block_o,
    block_t = excluded.block_t,
    block_l = excluded.block_l,
    block_j = excluded.block_j,
    block_s = excluded.block_s,
    block_z = excluded.block_z,
    total_moves = excluded.total_moves,
    total_rotations = excluded.total_rotations,
    hard_drops = excluded.hard_drops,
    soft_drops = excluded.soft_drops,
    hold_count = excluded.hold_count,
    duration_seconds = excluded.duration_seconds,
    apm = excluded.apm,
    status = excluded.status,
    updated_at = datetime('now', 'localtime');
`);

function saveTelemetry(payload) {
  const duration = Math.max(0, Number(payload.duration_seconds) || 0);
  const apmDuration = Math.max(1, duration);
  const moves = Number(payload.total_moves) || 0;
  const rotations = Number(payload.total_rotations) || 0;
  const hardDrops = Number(payload.hard_drops) || 0;
  const softDrops = Number(payload.soft_drops) || 0;
  const holds = Number(payload.hold_count) || 0;
  const totalActions = moves + rotations + hardDrops + softDrops + holds;
  const computedApm = Number(payload.apm) || (totalActions > 0 ? Math.round(((totalActions / apmDuration) * 60) * 10) / 10 : 0);

  const gameId = String(payload.game_id || payload.session_id || `OYUN-${Date.now().toString(36).toUpperCase()}`);

  upsertStmt.run({
    $session_id: gameId,
    $player_name: String(payload.player_name || gameId),
    $device_id: String(payload.device_id || 'local_device'),
    $platform: String(payload.platform || 'UnityEditor'),
    $score: Number(payload.score) || 0,
    $max_level: Math.max(1, Number(payload.max_level) || 1),
    $max_combo: Number(payload.max_combo) || 0,
    $lines_cleared: Number(payload.lines_cleared) || 0,
    $singles: Number(payload.singles) || 0,
    $doubles: Number(payload.doubles) || 0,
    $triples: Number(payload.triples) || 0,
    $tetrises: Number(payload.tetrises) || 0,
    $blocks_spawned: Number(payload.blocks_spawned) || 0,
    $block_i: Number(payload.block_i) || 0,
    $block_o: Number(payload.block_o) || 0,
    $block_t: Number(payload.block_t) || 0,
    $block_l: Number(payload.block_l) || 0,
    $block_j: Number(payload.block_j) || 0,
    $block_s: Number(payload.block_s) || 0,
    $block_z: Number(payload.block_z) || 0,
    $total_moves: moves,
    $total_rotations: rotations,
    $hard_drops: hardDrops,
    $soft_drops: softDrops,
    $hold_count: holds,
    $duration_seconds: Math.round(duration * 10) / 10,
    $apm: computedApm,
    $status: String(payload.status || 'active')
  });
}

db.exec(`DELETE FROM sessions WHERE session_id LIKE 'demo_%' OR session_id LIKE 'unity_sim_%';`);

function getDashboardStats() {
  const summary = db.prepare(`
    SELECT
      COUNT(*) AS total_sessions,
      COALESCE(MAX(score), 0) AS high_score,
      COALESCE(ROUND(AVG(score), 0), 0) AS avg_score,
      COALESCE(MAX(max_combo), 0) AS best_combo,
      COALESCE(MAX(max_level), 1) AS highest_level,
      COALESCE(SUM(lines_cleared), 0) AS total_lines,
      COALESCE(SUM(blocks_spawned), 0) AS total_blocks,
      COALESCE(ROUND(AVG(duration_seconds), 1), 0) AS avg_duration,
      COALESCE(ROUND(AVG(apm), 1), 0) AS avg_apm,
      COALESCE(SUM(singles), 0) AS total_singles,
      COALESCE(SUM(doubles), 0) AS total_doubles,
      COALESCE(SUM(triples), 0) AS total_triples,
      COALESCE(SUM(tetrises), 0) AS total_tetrises,
      COALESCE(SUM(block_i), 0) AS sum_i,
      COALESCE(SUM(block_o), 0) AS sum_o,
      COALESCE(SUM(block_t), 0) AS sum_t,
      COALESCE(SUM(block_l), 0) AS sum_l,
      COALESCE(SUM(block_j), 0) AS sum_j,
      COALESCE(SUM(block_s), 0) AS sum_s,
      COALESCE(SUM(block_z), 0) AS sum_z,
      COALESCE(SUM(total_moves), 0) AS sum_moves,
      COALESCE(SUM(total_rotations), 0) AS sum_rotations,
      COALESCE(SUM(hard_drops), 0) AS sum_hard_drops,
      COALESCE(SUM(soft_drops), 0) AS sum_soft_drops,
      COALESCE(SUM(hold_count), 0) AS sum_holds
    FROM sessions
  `).get();

  const recentSessions = db.prepare(`
    SELECT * FROM sessions
    ORDER BY id DESC
    LIMIT 15
  `).all();

  const leaderboard = db.prepare(`
    SELECT
      session_id, player_name, platform, score, max_level, max_combo,
      lines_cleared, tetrises, apm, duration_seconds, updated_at, status
    FROM sessions
    ORDER BY score DESC, lines_cleared DESC
    LIMIT 10
  `).all();

  const platformBreakdown = db.prepare(`
    SELECT platform, COUNT(*) AS count, ROUND(AVG(score), 0) AS avg_score
    FROM sessions
    GROUP BY platform
    ORDER BY count DESC
  `).all();

  return {
    summary,
    recentSessions,
    leaderboard,
    platformBreakdown
  };
}

const MIME_TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.js': 'application/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon'
};

function sendJson(res, statusCode, data) {
  res.writeHead(statusCode, {
    'Content-Type': 'application/json; charset=utf-8',
    'Access-Control-Allow-Origin': '*',
    'Access-Control-Allow-Methods': 'GET, POST, DELETE, OPTIONS',
    'Access-Control-Allow-Headers': 'Content-Type'
  });
  res.end(JSON.stringify(data));
}

const server = http.createServer((req, res) => {
  if (req.method === 'OPTIONS') {
    res.writeHead(204, {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Methods': 'GET, POST, DELETE, OPTIONS',
      'Access-Control-Allow-Headers': 'Content-Type'
    });
    return res.end();
  }

  const parsedUrl = new URL(req.url, `http://${req.headers.host || 'localhost'}`);
  const pathname = parsedUrl.pathname;

  if (pathname === '/api/stats' && req.method === 'GET') {
    try {
      return sendJson(res, 200, { ok: true, data: getDashboardStats() });
    } catch (err) {
      return sendJson(res, 500, { ok: false, error: err.message });
    }
  }

  if (pathname === '/api/telemetry' && req.method === 'POST') {
    let body = '';
    req.on('data', chunk => {
      body += chunk.toString();
      if (body.length > 1e6) req.socket.destroy();
    });
    req.on('end', () => {
      try {
        const payload = JSON.parse(body || '{}');
        saveTelemetry(payload);
        const gid = payload.game_id || payload.session_id;
        console.log(`[TELEMETRY] Oyun ID: ${gid} | Skor: ${payload.score} | Sure: ${payload.duration_seconds}s | Durum: ${payload.status}`);
        return sendJson(res, 200, { ok: true, game_id: gid, message: 'Telemetry kaydedildi' });
      } catch (err) {
        console.error('[TELEMETRY ERROR]', err.message);
        return sendJson(res, 400, { ok: false, error: err.message });
      }
    });
    return;
  }

  if (pathname === '/api/reset' && req.method === 'DELETE') {
    try {
      db.exec('DELETE FROM sessions;');
      return sendJson(res, 200, { ok: true, data: getDashboardStats() });
    } catch (err) {
      return sendJson(res, 500, { ok: false, error: err.message });
    }
  }

  let filePath = pathname === '/' ? path.join(PUBLIC_DIR, 'index.html') : path.join(PUBLIC_DIR, pathname);
  if (!filePath.startsWith(PUBLIC_DIR)) {
    res.writeHead(403);
    return res.end('Forbidden');
  }

  fs.readFile(filePath, (err, content) => {
    if (err) {
      res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
      return res.end('404 Not Found');
    }
    const ext = path.extname(filePath).toLowerCase();
    res.writeHead(200, {
      'Content-Type': MIME_TYPES[ext] || 'application/octet-stream',
      'Cache-Control': 'no-cache'
    });
    res.end(content);
  });
});

server.listen(PORT, () => {
  console.log(`Sunucu calisiyor: http://localhost:${PORT}`);
  console.log(`Unity API: http://localhost:${PORT}/api/telemetry`);
});
