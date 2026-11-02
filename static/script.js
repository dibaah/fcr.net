/* ═══════════════════════════════════════════════════════════════
   FCR Dashboard — Frontend Application
   ═══════════════════════════════════════════════════════════════ */
(function() {
const API = '';
const $ = (s, r=document) => r.querySelector(s);
const $$ = (s, r=document) => [...r.querySelectorAll(s)];

const state = {
  records: [], originals: new Map(), edits: new Map(), selected: new Set(),
  options: {}, settings: {}, charts: {}, undoStack: [], redoStack: [],
  sortBy: 'date', sortOrder: 'desc', statusGroup: null,
  notifications: [],
  lang: 'en',
};

/* ═══ i18n Translations ═════════════════════════════════════ */
const translations = {
  en: {
    app_title: "FCR Dashboard",
    company: "SINOPEC International — YADAVARAN",
    login_welcome: "Welcome",
    login_sub: "Facility Change Request Control Center",
    login_starting: "Starting local server and preparing database...",
    login_connecting: "Connecting to backend...",
    login_loading_settings: "Loading settings...",
    login_loading_filters: "Loading filters and dashboard...",
    login_loading_records: "Loading FCR records...",
    login_ready: "Ready.",
    login_enter: "Enter Dashboard",
    login_signin_title: "Sign in",
    login_signin_sub: "Use your corporate account to continue",
    login_username: "Username",
    login_password: "Password",
    login_remember: "Remember me on this computer",
    login_button: "Sign In",
    login_internal: "Internal Use Only",
    login_missing: "Please enter both your username and password.",
    login_invalid: "Invalid username or password.",
    nav_dashboard: "Dashboard",
    nav_records: "FCR Records",
    nav_add: "Add New FCR",
    nav_notifications: "Notifications",
    nav_reports: "Reports",
    nav_settings: "Settings",
    nav_about: "About",
    dashboard: "Dashboard",
    dashboard_sub: "Overview of Facility Change Requests",
    total_fcrs: "Total FCRs",
    open_active: "Open / Active",
    finished_closed: "Finished / Closed",
    in_progress: "In Progress",
    rejected: "Rejected",
    total: "Total",
    open: "Open",
    closed: "Closed",
    unsaved: "unsaved",
    search_placeholder: "🔍 Search code, subject, originator...",
    export: "Export",
    export_report: "Export Full Report to Excel",
    columns: "Columns",
    show_columns: "Show Columns",
    preset_minimal: "Minimal",
    preset_standard: "Standard",
    preset_full: "Full",
    filter_pedec: "PEDEC:",
    filter_fcr: "FCR Status:",
    filter_exec: "Execution:",
    filter_category: "Category:",
    all: "All",
    reset: "Reset",
    reset_sort: "Reset Sort",
    save_all: "Save All",
    discard: "Discard",
    delete_selected: "Delete Selected",
    actions: "Actions",
    cancel: "Cancel",
    delete: "Delete",
    save_fcr: "Save FCR",
    save_changes: "Save Changes",
    records: "FCR Records",
    records_sub: "Click any cell to edit. Changes are saved with Save All.",
    add_new: "Add New FCR",
    add_new_sub: "Fill in the details below. FCR Code must be unique.",
    notifications: "Notifications",
    notif_sub: "PEDEC Follow-up Alerts",
    notif_page_sub: "PEDEC Follow-up Alerts — Issued FCRs waiting longer than the configured threshold.",
    pedec_alerts: "PEDEC Follow-up Alerts",
    pedec_alerts_desc: "Issued FCRs waiting longer than the configured threshold.",
    reports: "Reports",
    reports_sub: "Statistical summary of the FCR database",
    status_distribution: "Status Distribution",
    trend_year: "Trend by Year",
    settings: "Settings",
    settings_sub: "Application configuration",
    set_company: "🏢 Company",
    company_name: "Company Name",
    set_database: "🗄 Database",
    db_location: "Database Location",
    set_folder: "📁 Folder Settings",
    parent_path: "Parent Folder Path",
    set_backup: "💾 Backup",
    backup_folder: "Backup Folder",
    resolved_location: "Resolved Location",
    backup_now: "Backup Database Now",
    view_backups: "View Backups",
    set_appearance: "🎨 Appearance",
    dark_mode: "Dark Mode",
    enable_dark: "Enable dark theme",
    reset_defaults: "Reset to Defaults",
    save_settings: "Save Settings",
    chart_year: "📅 FCRs by Year",
    chart_category: "📂 By Category",
    chart_exec: "⚙️ Execution Status",
    chart_action: "👥 Top Action Parties",
    no_overdue: "No overdue items.",
    loading_notif: "Loading notifications...",
    days_overdue: "days overdue",
    nav_requests: "My Requests",
    nav_admin_requests: "Request Queue",
    my_requests: "My Requests",
    my_requests_sub: "Submit a new FCR request and track its status.",
    new_request: "New FCR Request",
    req_subject: "Subject *",
    req_justification: "Justification *",
    req_description: "Description *",
    req_attachments: "Attachments (optional)",
    submit_request: "Submit Request",
    request_history: "Request History",
    req_number: "Request #",
    status: "Status",
    submitted_at: "Submitted",
    requester: "Requester",
    request_queue: "Request Queue",
    request_queue_sub: "Incoming FCR requests — review, assign codes, prepare workflow.",
    req_submitted: "Request submitted",
    comments: "Comments",
    add_comment: "Add comment",
    attachments: "Attachments",
    upload: "Upload",
    download: "Download",
    save_draft: "Save",
    create_fcr_record: "Create linked FCR",
    review_mode: "Review mode",
    reviewers: "Reviewers",
    decision_rule: "Decision rule",
    prepare_workflow: "Save workflow",
    submit_for_review: "Submit for review",
    workflow: "Workflow",
    no_reviewers: "No users with the Reviewer role — create some in Admin.",
    nav_reviews: "My Reviews",
    my_reviews: "My Reviews",
    my_reviews_sub: "Requests assigned to you for review.",
    stage: "Stage",
    cycle: "Cycle",
    review: "Review",
    approve: "Approve",
    reject: "Reject",
    return_for_correction: "Return for correction",
    comment_optional: "Comment (optional)",
    submit_decision: "Submit decision",
    review_history: "Review history",
    resubmit: "Resubmit for review",
    notifications_wf: "Workflow notifications",
    mark_read: "Mark read",
  },
  zh: {
    app_title: "FCR 数据库仪表板",
    company: "中石化国际 — YADAVARAN",
    login_welcome: "欢迎",
    login_sub: "设施变更请求控制中心",
    login_starting: "正在启动本地服务器并准备数据库...",
    login_connecting: "正在连接后端...",
    login_loading_settings: "正在加载设置...",
    login_loading_filters: "正在加载筛选器和仪表板...",
    login_loading_records: "正在加载 FCR 记录...",
    login_ready: "准备就绪。",
    login_enter: "进入仪表板",
    login_signin_title: "登录",
    login_signin_sub: "请使用您的企业帐户继续",
    login_username: "用户名",
    login_password: "密码",
    login_remember: "在此计算机上记住我",
    login_button: "登录",
    login_internal: "仅限内部使用",
    login_missing: "请输入用户名和密码。",
    login_invalid: "用户名或密码无效。",
    nav_dashboard: "仪表板",
    nav_records: "FCR 记录",
    nav_add: "添加新 FCR",
    nav_notifications: "通知",
    nav_reports: "报告",
    nav_settings: "设置",
    nav_about: "关于",
    dashboard: "仪表板",
    dashboard_sub: "设施变更请求概览",
    total_fcrs: "FCR 总数",
    open_active: "打开/活跃",
    finished_closed: "已完成/已关闭",
    in_progress: "进行中",
    rejected: "已拒绝",
    total: "总计",
    open: "打开",
    closed: "已关闭",
    unsaved: "未保存",
    search_placeholder: "🔍 搜索代码、主题、发起人...",
    export: "导出",
    export_report: "导出完整报告到 Excel",
    columns: "列",
    show_columns: "显示列",
    preset_minimal: "最小",
    preset_standard: "标准",
    preset_full: "完整",
    filter_pedec: "PEDEC:",
    filter_fcr: "FCR 状态:",
    filter_exec: "执行:",
    filter_category: "类别:",
    all: "全部",
    reset: "重置",
    reset_sort: "重置排序",
    save_all: "全部保存",
    discard: "放弃",
    delete_selected: "删除选中",
    actions: "操作",
    cancel: "取消",
    delete: "删除",
    save_fcr: "保存 FCR",
    save_changes: "保存更改",
    records: "FCR 记录",
    records_sub: "点击任意单元格编辑。通过\"全部保存\"保存更改。",
    add_new: "添加新 FCR",
    add_new_sub: "填写以下详细信息。FCR 代码必须唯一。",
    notifications: "通知",
    notif_sub: "PEDEC 跟进提醒",
    notif_page_sub: "PEDEC 跟进提醒 — 已发出 FCR 等待时间超过配置的阈值。",
    pedec_alerts: "PEDEC 跟进提醒",
    pedec_alerts_desc: "已发出 FCR 等待时间超过配置的阈值。",
    reports: "报告",
    reports_sub: "FCR 数据库统计摘要",
    status_distribution: "状态分布",
    trend_year: "按年份趋势",
    settings: "设置",
    settings_sub: "应用程序配置",
    set_company: "🏢 公司",
    company_name: "公司名称",
    set_database: "🗄 数据库",
    db_location: "数据库位置",
    set_folder: "📁 文件夹设置",
    parent_path: "父文件夹路径",
    set_backup: "💾 备份",
    backup_folder: "备份文件夹",
    resolved_location: "解析位置",
    backup_now: "立即备份数据库",
    view_backups: "查看备份",
    set_appearance: "🎨 外观",
    dark_mode: "深色模式",
    enable_dark: "启用深色主题",
    reset_defaults: "恢复默认",
    save_settings: "保存设置",
    chart_year: "📅 按年份统计",
    chart_category: "📂 按类别",
    chart_exec: "⚙️ 执行状态",
    chart_action: "👥 主要责任方",
    no_overdue: "无逾期项目。",
    loading_notif: "正在加载通知...",
    days_overdue: "天逾期",
    nav_requests: "我的请求",
    nav_admin_requests: "请求队列",
    my_requests: "我的请求",
    my_requests_sub: "提交新的 FCR 请求并跟踪其状态。",
    new_request: "新建 FCR 请求",
    req_subject: "主题 *",
    req_justification: "理由 *",
    req_description: "描述 *",
    req_attachments: "附件（可选）",
    submit_request: "提交请求",
    request_history: "请求历史",
    req_number: "请求编号",
    status: "状态",
    submitted_at: "提交时间",
    requester: "请求人",
    request_queue: "请求队列",
    request_queue_sub: "待处理的 FCR 请求 — 审核、分配代码、准备工作流。",
    req_submitted: "请求已提交",
    comments: "评论",
    add_comment: "添加评论",
    attachments: "附件",
    upload: "上传",
    download: "下载",
    save_draft: "保存",
    create_fcr_record: "创建关联 FCR",
    review_mode: "评审模式",
    reviewers: "评审人",
    decision_rule: "决策规则",
    prepare_workflow: "保存工作流",
    submit_for_review: "提交评审",
    workflow: "工作流",
    no_reviewers: "没有具有评审人角色的用户 — 请在管理中创建。",
    nav_reviews: "我的评审",
    my_reviews: "我的评审",
    my_reviews_sub: "分配给您评审的请求。",
    stage: "阶段",
    cycle: "周期",
    review: "评审",
    approve: "批准",
    reject: "拒绝",
    return_for_correction: "退回修改",
    comment_optional: "评论（可选）",
    submit_decision: "提交决定",
    review_history: "评审历史",
    resubmit: "重新提交评审",
    notifications_wf: "工作流通知",
    mark_read: "标记已读",
  }
};

/* ═══ i18n Functions ════════════════════════════════════════ */
function applyLanguage(lang) {
  state.lang = lang;
  const t = translations[lang] || translations.en;
  document.querySelectorAll('[data-i18n]').forEach(el => {
    const key = el.getAttribute('data-i18n');
    if (t[key]) el.textContent = t[key];
  });
  document.querySelectorAll('[data-i18n-placeholder]').forEach(el => {
    const key = el.getAttribute('data-i18n-placeholder');
    if (t[key]) el.placeholder = t[key];
  });
  // Update language menu active state
  $$('.lang-option').forEach(opt => {
    opt.classList.toggle('active', opt.dataset.lang === lang);
  });
  localStorage.setItem('fcr_language', lang);
}

function t(key) {
  return (translations[state.lang] || translations.en)[key] || key;
}

/* ═══ Toast ═════════════════════════════════════════════════ */
function toast(msg, type='info', ms=3000) {
  const el = document.createElement('div');
  el.className = 'toast ' + type;
  el.textContent = msg;
  $('#toast-container').appendChild(el);
  setTimeout(() => { el.style.opacity = '0'; setTimeout(() => el.remove(), 300); }, ms);
}

/* ═══ Confirm Dialog ════════════════════════════════════════ */
function confirmDlg(title, msg) {
  return new Promise(res => {
    $('#modal-title').textContent = title;
    $('#modal-msg').textContent = msg;
    $('#modal').classList.add('show');
    const ok = () => { cleanup(); res(true); };
    const no = () => { cleanup(); res(false); };
    const cleanup = () => {
      $('#modal').classList.remove('show');
      $('#modal-ok').removeEventListener('click', ok);
      $('#modal-cancel').removeEventListener('click', no);
    };
    $('#modal-ok').addEventListener('click', ok);
    $('#modal-cancel').addEventListener('click', no);
  });
}

/* ═══ API Helper ════════════════════════════════════════════ */
async function api(path, opts = {}) {
  try {
    const r = await fetch(API + path, opts);
    if (r.status === 401) { location.reload(); }
    if (!r.ok) { const txt = await r.text(); throw new Error(txt || r.statusText); }
    const ct = r.headers.get('content-type') || '';
    return ct.includes('json') ? r.json() : r;
  } catch (e) { toast('Request failed: ' + e.message, 'error'); throw e; }
}

/* Pretty-print a FastAPI error body ({"detail": "..."}) for toasts */
function errorText(err) {
  try {
    const parsed = JSON.parse(err.message);
    if (parsed && parsed.detail) return String(parsed.detail);
  } catch (e) { /* not JSON */ }
  return err.message;
}

/* ═══ Navigation ════════════════════════════════════════════ */
const PAGES = ['dashboard', 'records', 'add', 'notifications', 'requests', 'admin-requests', 'reviews', 'reports', 'settings', 'about'];

/* Single place that switches pages, so nav clicks, deep links (#page-…)
   and notification shortcuts all behave identically. */
function activatePage(page, { silentHash = false } = {}) {
  if (!PAGES.includes(page)) return false;
  const target = $('#page-' + page);
  const nav = $(`.nav-item[data-page="${page}"]`);
  if (!target || !nav) return false;

  $$('.nav-item').forEach(x => x.classList.remove('active'));
  nav.classList.add('active');
  $$('.page').forEach(x => x.classList.remove('active'));
  target.classList.add('active');

  if (!silentHash) {
    try { history.replaceState(null, '', '#' + page); } catch (e) {}
  }

  if (page === 'dashboard') loadDashboard();
  if (page === 'records') loadRecords();
  if (page === 'reports') loadReports();
  if (page === 'settings') loadSettings();
  if (page === 'about') loadAbout();
  if (page === 'notifications') loadNotificationsPage();
  if (page === 'requests') loadRequestsPage();
  if (page === 'admin-requests') loadAdminRequestsPage();
  if (page === 'reviews') loadReviewsPage();
  return true;
}

function goToRecords(searchText) {
  if (searchText !== undefined) $('#f-search').value = searchText;
  state.statusGroup = null;
  activeQuickFilter = null;
  $$('.quick-stat').forEach(x => x.classList.remove('active'));
  activatePage('records');
}

$$('.nav-item').forEach(n => {
  n.addEventListener('click', () => activatePage(n.dataset.page));
});

/* ═══ Sidebar Toggle ════════════════════════════════════════ */
$('#btnToggleSidebar')?.addEventListener('click', () => {
  $('.app')?.classList.toggle('sidebar-collapsed');
});

/* ═══ Language Dropdown ═════════════════════════════════════ */
$('#btnLang')?.addEventListener('click', (e) => {
  e.stopPropagation();
  $('#lang-dropdown .dropdown-menu').classList.toggle('show');
  $('#notif-menu').classList.remove('show');
});
$$('.lang-option').forEach(opt => {
  opt.addEventListener('click', () => {
    applyLanguage(opt.dataset.lang);
    $('#lang-dropdown .dropdown-menu').classList.remove('show');
    toast(state.lang === 'zh' ? '语言已切换为中文' : 'Language switched to English', 'success', 1500);
  });
});

/* ═══ Notifications Dropdown ════════════════════════════════ */
$('#btnNotifications')?.addEventListener('click', async (e) => {
  e.stopPropagation();
  const menu = $('#notif-menu');
  menu.classList.toggle('show');
  $('#lang-dropdown .dropdown-menu').classList.remove('show');
  if (menu.classList.contains('show')) {
    await loadNotificationsDropdown();
  }
});

async function loadNotificationsDropdown() {
  const list = $('#notif-list');
  list.innerHTML = `<span class="text-muted">${t('loading_notif')}</span>`;
  try {
    const data = await api('/api/notifications');
    state.notifications = data.items || [];
    const btn = $('#btnNotifications');
    const count = $('#notifCount');
    if (count) count.textContent = data.count || 0;
    if (btn) btn.classList.toggle('has-items', (data.count || 0) > 0);

    if (!state.notifications.length) {
      list.innerHTML = `<div class="notif-empty">${t('no_overdue')}</div>`;
    } else {
      list.innerHTML = state.notifications.slice(0, 15).map(n => `
        <div class="notif-item" data-id="${n.id}" data-fcr-code="${escapeHtml(n.fcr_code || '')}">
          <strong>${escapeHtml(n.fcr_code || 'No code')} · ${n.age_days} ${t('days_overdue')}</strong>
          <span>${escapeHtml(n.fcr_subject || '')}</span>
          <div class="notif-meta">
            <span>${escapeHtml(n.action_party || '')}</span>
            <span>${escapeHtml(n.date || '')}</span>
          </div>
        </div>
      `).join('');
      // Click on notification → go to records and filter
      $$('#notif-list .notif-item').forEach(item => {
        item.addEventListener('click', () => {
          $('#notif-menu').classList.remove('show');
          goToRecords(item.dataset.fcrCode || '');
        });
      });
    }
  } catch (e) {
    list.innerHTML = `<div class="notif-empty">Failed to load notifications</div>`;
  }
}

/* Close dropdowns when clicking outside */
document.addEventListener('click', (e) => {
  if (!e.target.closest('#lang-dropdown')) $('#lang-dropdown .dropdown-menu').classList.remove('show');
  if (!e.target.closest('#notif-dropdown')) $('#notif-menu').classList.remove('show');
  if (!e.target.closest('.dropdown')) $('#column-menu').classList.remove('show');
});

/* ═══ Theme ═════════════════════════════════════════════════ */
function applyTheme(dark) {
  document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
  const btn = $('#btnTheme');
  if (btn) btn.textContent = dark ? '☀' : '🌙';
}

/* Re-draw whichever charts are on screen so they pick up the new theme colours */
function refreshCharts() {
  if ($('#page-dashboard')?.classList.contains('active')) loadDashboard();
  if ($('#page-reports')?.classList.contains('active')) loadReports();
}

$('#btnTheme').addEventListener('click', async () => {
  const cur = document.documentElement.getAttribute('data-theme') === 'dark';
  applyTheme(!cur);
  state.settings.dark_mode = !cur;
  refreshCharts();
  try { await api('/api/settings', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ dark_mode: !cur }) }); } catch (e) {}
});

/* ═══ Dirty State ═══════════════════════════════════════════ */
function updateDirty() {
  const n = state.edits.size;
  $('#dirtyCount').textContent = n;
  $('#dirtyChip').classList.toggle('show', n > 0);
}
function markDirty(id) {
  const orig = state.originals.get(id);
  const cur = state.records.find(r => r.id === id);
  if (!orig || !cur) return;
  let changed = false;
  for (const k of Object.keys(orig)) {
    if ((orig[k] || '') !== (cur[k] || '')) { changed = true; break; }
  }
  if (changed) state.edits.set(id, { ...cur });
  else state.edits.delete(id);
  const row = document.querySelector(`tr[data-id="${id}"]`);
  if (row) row.classList.toggle('dirty', changed);
  updateDirty();
}

/* ═══ Undo / Redo ═══════════════════════════════════════════ */
/* Undo/redo only change the local buffer; nothing reaches the server until
   the user presses Save All (matching the rest of the table workflow). */
function pushUndo(id) {
  const cur = state.records.find(r => r.id === id);
  if (!cur) return;
  state.undoStack.push({ id, snap: { ...cur } });
  state.redoStack = [];
  if (state.undoStack.length > 100) state.undoStack.shift();
}
function undo() {
  const a = state.undoStack.pop();
  if (!a) { toast('Nothing to undo', 'warn', 1500); return; }
  const rec = state.records.find(r => r.id === a.id);
  if (!rec) return;
  state.redoStack.push({ id: a.id, snap: { ...rec } });
  Object.assign(rec, a.snap);
  renderRow(rec);
  markDirty(a.id);
  toast('↶ Undone — use Save All to apply', 'info', 2000);
}
function redo() {
  const a = state.redoStack.pop();
  if (!a) { toast('Nothing to redo', 'warn', 1500); return; }
  const rec = state.records.find(r => r.id === a.id);
  if (!rec) return;
  state.undoStack.push({ id: a.id, snap: { ...rec } });
  Object.assign(rec, a.snap);
  renderRow(rec);
  markDirty(a.id);
  toast('↷ Redone — use Save All to apply', 'info', 2000);
}
$('#btnUndo').addEventListener('click', undo);
$('#btnRedo').addEventListener('click', redo);
document.addEventListener('keydown', e => {
  const tag = (e.target.tagName || '').toLowerCase();
  const typing = tag === 'input' || tag === 'textarea' || tag === 'select'
    || e.target.isContentEditable === true;
  if (!typing && (e.ctrlKey || e.metaKey) && e.key === 'z' && !e.shiftKey) { e.preventDefault(); undo(); }
  if (!typing && (e.ctrlKey || e.metaKey) && (e.key === 'y' || (e.key === 'z' && e.shiftKey))) { e.preventDefault(); redo(); }
  if ((e.ctrlKey || e.metaKey) && e.key === 's') { e.preventDefault(); saveAll(); }
  if (e.key === 'Enter' && !typing && state.selected.size === 1) {
    const id = [...state.selected][0];
    openEditModal(id);
  }
});

/* ═══ Helpers ═══════════════════════════════════════════════ */
function pill(text) {
  if (!text) return '<span class="status-pill pill-gray">—</span>';
  const t = text.toLowerCase();
  let cls = 'pill-gray';
  if (/finished|closed|approved/.test(t)) cls = 'pill-green';
  else if (/rejected|suspended|cancelled/.test(t)) cls = 'pill-red';
  else if (/pending|awaiting|on-going|preparation|issued|evaluation|commented/.test(t)) cls = 'pill-yellow';
  return `<span class="status-pill ${cls}">${escapeHtml(text)}</span>`;
}
function escapeHtml(s) {
  if (s == null) return '';
  return String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}
function dateOnly(value) {
  const s = (value || '').toString().trim();
  return /^\d{4}-\d{2}-\d{2}/.test(s) ? s.slice(0, 10) : s;
}

/* ═══ Options ═══════════════════════════════════════════════ */
async function loadOptions() {
  try {
    state.options = await api('/api/options');
    fillSelect('#f-pedec', state.options.pedec_status, 'All PEDEC', true);
    fillSelect('#f-fcr', state.options.fcr_status, 'All FCR Status', true);
    fillSelect('#f-exec', state.options.execution_status, 'All Execution', true);
    fillSelect('#f-cat', state.options.category, 'All Category', true);
    fillSelect('#sel-pedec-add', state.options.pedec_status, '—', true);
    fillSelect('#sel-fcr-add', state.options.fcr_status, '—', true);
    fillSelect('#sel-exec-add', state.options.execution_status, '—', true);
  } catch (e) {}
}
function fillSelect(sel, items, placeholder, preserveValue) {
  const el = $(sel); if (!el) return;
  const prev = preserveValue ? el.value : null;
  el.innerHTML = `<option value="">${placeholder}</option>` +
    (items || []).map(v => `<option>${escapeHtml(v)}</option>`).join('');
  if (prev !== null && prev !== '') {
    const opt = el.querySelector(`option[value="${prev}"]`) || [...el.options].find(o => o.textContent === prev);
    if (opt) opt.selected = true;
  }
}

/* ═══ Dashboard ═════════════════════════════════════════════ */
async function loadDashboard() {
  try {
    const s = await api('/api/stats');
    $('#st-total').textContent = s.total;
    $('#st-open').textContent = s.open;
    $('#st-done').textContent = s.finished;
    $('#st-pend').textContent = s.pending;
    drawChart('ch-year', 'bar', s.trend.map(x => x.yr), s.trend.map(x => x.cnt), '#3b82f6');
    drawChart('ch-cat', 'doughnut', s.by_category.map(x => x.category), s.by_category.map(x => x.cnt));
    drawChart('ch-exec', 'doughnut', s.by_exec_status.map(x => x.execution_status), s.by_exec_status.map(x => x.cnt));
    drawChart('ch-ap', 'bar', s.by_action_party.map(x => x.action_party), s.by_action_party.map(x => x.cnt), '#1e3c72', true);
  } catch (e) {}
}

function drawChart(id, type, labels, data, color, horiz) {
  if (state.charts[id]) state.charts[id].destroy();
  const ctx = document.getElementById(id);
  if (!ctx) return;
  const palette = ['#3b82f6', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#ec4899', '#14b8a6', '#f97316'];
  const isDark = document.documentElement.getAttribute('data-theme') === 'dark';
  const textColor = isDark ? '#e5e7eb' : '#334155';
  const gridColor = isDark ? '#1f2937' : '#e2e8f0';
  state.charts[id] = new Chart(ctx, {
    type,
    data: { labels, datasets: [{ label: '', data, backgroundColor: type === 'bar' ? (color || '#3b82f6') : palette, borderColor: type === 'bar' ? (color || '#3b82f6') : '#fff', borderWidth: type === 'bar' ? 0 : 2, borderRadius: type === 'bar' ? 4 : 0 }] },
    options: {
      responsive: true, maintainAspectRatio: false, indexAxis: horiz ? 'y' : 'x',
      plugins: {
        legend: { display: type !== 'bar', position: type === 'doughnut' ? 'right' : 'top', labels: { color: textColor, font: { size: 11 }, padding: 12, usePointStyle: true } },
        tooltip: { backgroundColor: isDark ? '#1e293b' : '#fff', titleColor: isDark ? '#e5e7eb' : '#0f172a', bodyColor: isDark ? '#cbd5e1' : '#475569', borderColor: isDark ? '#334155' : '#e2e8f0', borderWidth: 1, padding: 10, cornerRadius: 6 }
      },
      scales: type === 'bar' ? {
        x: { ticks: { color: textColor, font: { size: 10 } }, grid: { color: gridColor, drawBorder: false }, border: { display: false } },
        y: { ticks: { color: textColor, font: { size: 10 } }, grid: { color: gridColor, drawBorder: false }, border: { display: false }, beginAtZero: true }
      } : {},
      layout: { padding: { top: 5, bottom: 5, left: 5, right: 5 } }
    }
  });
}

/* ═══ Records ═══════════════════════════════════════════════ */
let searchTimer;
$('#f-search').addEventListener('input', e => {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => loadRecords(), 250);
});
['#f-pedec', '#f-fcr', '#f-exec', '#f-cat'].forEach(s => {
  $(s).addEventListener('change', () => {
    // A manual filter overrides the quick-stat group, otherwise the two
    // filters would silently stack and show confusing counts.
    if (state.statusGroup) {
      state.statusGroup = null;
      activeQuickFilter = null;
      $$('.quick-stat').forEach(x => x.classList.remove('active'));
    }
    loadRecords();
  });
});
$('#btn-reset').addEventListener('click', () => {
  $('#f-search').value = '';
  $('#f-pedec').value = ''; $('#f-fcr').value = '';
  $('#f-exec').value = ''; $('#f-cat').value = '';
  clearQuickFilter();
});


function renderTable() {
  const tb = $('#tbody-fcr');
  if (!tb) return;

  // Clear and inject newly updated rows from our state array
  tb.innerHTML = state.records.map(r => rowHtml(r)).join('');
  
  // Re-bind all inline edit triggers and click handlers to the new rows
  tb.querySelectorAll('tr').forEach(tr => {
    bindRowEvents(tr, +tr.dataset.id);
  });

  // 💡 FORCE VISIBILITY RE-ALIGNMENT: Ensures hidden columns stay hidden immediately after render
  if (typeof applyColumnVisibility === 'function') {
    applyColumnVisibility();
  }
}

async function loadRecords() {
  const p = new URLSearchParams();
  if ($('#f-search').value) p.set('search', $('#f-search').value);
  if ($('#f-pedec').value) p.set('pedec_status', $('#f-pedec').value);
  if ($('#f-fcr').value) p.set('fcr_status', $('#f-fcr').value);
  if ($('#f-exec').value) p.set('exec_status', $('#f-exec').value);
  if ($('#f-cat').value) p.set('category', $('#f-cat').value);
  if (state.statusGroup) p.set('status_group', state.statusGroup);
  p.set('sort_by', state.sortBy);
  p.set('sort_order', state.sortOrder);

  try {
    const data = await api('/api/fcr?' + p.toString());

    /* Un-saved edits survive filtering/searching/sorting: the server rows
       become the new "originals" and any pending edit is re-applied on top. */
    const pendingEdits = new Map(state.edits);
    const serverRows = data.map(r => ({
      ...r,
      date: dateOnly(r.date),
      review_date: dateOnly(r.review_date),
      closed_date: dateOnly(r.closed_date),
      last_update: dateOnly(r.last_update),
    }));

    state.originals = new Map(serverRows.map(r => [r.id, { ...r }]));
    state.records = serverRows.map(r => pendingEdits.has(r.id) ? { ...r, ...pendingEdits.get(r.id) } : r);

    state.edits = new Map(pendingEdits);
    for (const r of state.records) {
      const o = state.originals.get(r.id);
      let changed = false;
      if (o) for (const k of Object.keys(o)) { if ((o[k] || '') !== (r[k] || '')) { changed = true; break; } }
      if (changed) state.edits.set(r.id, { ...r }); else state.edits.delete(r.id);
    }
    state.selected.clear();

    updateDirty();
    renderTable();
    loadQuickStats();
  } catch (e) {
    console.error("Failed to fetch FCR records:", e);
  }
}

function sortBy(field) {
  if (state.sortBy === field) { state.sortOrder = state.sortOrder === 'asc' ? 'desc' : 'asc'; }
  else { state.sortBy = field; state.sortOrder = 'desc'; }
  updateSortIndicators();
  loadRecords();
}
function updateSortIndicators() {
  $$('#tbl-fcr thead th').forEach(th => {
    const indicator = th.querySelector('.sort-indicator');
    if (indicator) indicator.remove();
    th.classList.remove('sortable-active');
  });
  const activeTh = document.querySelector(`#tbl-fcr thead th[data-sort="${state.sortBy}"]`);
  if (activeTh) {
    activeTh.classList.add('sortable-active');
    const indicator = document.createElement('span');
    indicator.className = 'sort-indicator';
    indicator.textContent = state.sortOrder === 'asc' ? ' ▲' : ' ▼';
    activeTh.appendChild(indicator);
  }
}

/* ═══ Notifications ═════════════════════════════════════════ */
async function loadNotifications() {
  try {
    const data = await api('/api/notifications');
    state.notifications = data.items || [];
    const btn = $('#btnNotifications');
    const count = $('#notifCount');
    if (count) count.textContent = data.count || 0;
    if (btn) btn.classList.toggle('has-items', (data.count || 0) > 0);
  } catch (e) {}
}

/* ═══ Quick Filter Stats ════════════════════════════════════ */
/* The counters come straight from /api/stats and the rows come from
   /api/fcr?status_group=… — both use the same SQL conditions, so the
   number on the card always equals the number of rows you see. */
let activeQuickFilter = null;
async function loadQuickStats() {
  try {
    const s = await api('/api/stats');
    $('#qs-open').textContent = s.open;
    $('#qs-finished').textContent = s.finished;
    $('#qs-pending').textContent = s.pending;
    $('#qs-rejected').textContent = s.rejected;
  } catch (e) {}
}
$$('.quick-stat').forEach(stat => {
  stat.addEventListener('click', () => {
    const filter = stat.dataset.filter;
    if (activeQuickFilter === filter) { clearQuickFilter(); return; }
    activeQuickFilter = filter;
    state.statusGroup = filter;
    $$('.quick-stat').forEach(s => s.classList.remove('active'));
    stat.classList.add('active');
    $('#f-search').value = '';
    $('#f-pedec').value = ''; $('#f-fcr').value = '';
    $('#f-exec').value = ''; $('#f-cat').value = '';
    loadRecords();
  });
});
function clearQuickFilter() {
  activeQuickFilter = null;
  state.statusGroup = null;
  $$('.quick-stat').forEach(s => s.classList.remove('active'));
  $('#f-search').value = '';
  $('#f-pedec').value = ''; $('#f-fcr').value = '';
  $('#f-exec').value = ''; $('#f-cat').value = '';
  loadRecords();
}

/* ═══ Edit Modal ════════════════════════════════════════════ */
const EDIT_FIELDS = ['fcr_subject', 'fcr_code', 'category', 'date', 'review_date', 'originator', 'action_party', 'level', 'session_code', 'pedec_status', 'fcr_status', 'execution_status', 'closed_date', 'tr', 'next_action_by', 'arch_loc', 'remarks'];

function openEditModal(id) {
  const rec = state.records.find(r => r.id === id);
  if (!rec) { toast('Record not found', 'error'); return; }
  $('#edit-id').value = rec.id;
  $('#edit-modal-title').textContent = `Edit FCR — ${rec.fcr_code || '(no code)'}`;
  fillEditSelect('#edit-pedec_status', state.options.pedec_status, rec.pedec_status);
  fillEditSelect('#edit-fcr_status', state.options.fcr_status, rec.fcr_status);
  fillEditSelect('#edit-execution_status', state.options.execution_status, rec.execution_status);
  EDIT_FIELDS.forEach(f => { const el = $('#edit-' + f); if (el && el.tagName !== 'SELECT') el.value = rec[f] || ''; });
  $('#edit-modal').classList.add('show');
  setTimeout(() => $('#edit-fcr_subject').focus(), 100);
}
function fillEditSelect(sel, options, current) {
  const el = $(sel); if (!el) return;
  const set = new Set(options || []);
  if (current) set.add(current);
  el.innerHTML = '<option value="">—</option>' + [...set].map(v => `<option ${v === current ? 'selected' : ''}>${escapeHtml(v)}</option>`).join('');
}
function closeEditModal() { $('#edit-modal').classList.remove('show'); }
$('#btn-edit-cancel').addEventListener('click', closeEditModal);
$('#edit-modal').addEventListener('click', e => { if (e.target.id === 'edit-modal') closeEditModal(); });
document.addEventListener('keydown', e => { if (e.key === 'Escape' && $('#edit-modal').classList.contains('show')) closeEditModal(); });

$('#form-edit').addEventListener('submit', async e => {
  e.preventDefault();
  const id = +$('#edit-id').value;
  const rec = state.records.find(r => r.id === id);
  if (!rec) { toast('Record not found', 'error'); return; }
  pushUndo(id);
  const updated = { ...rec };
  EDIT_FIELDS.forEach(f => { const el = $('#edit-' + f); if (el) updated[f] = (el.value || '').trim(); });
  const btn = e.target.querySelector('button[type="submit"]');
  const origHTML = btn.innerHTML;
  btn.disabled = true; btn.innerHTML = '⏳ Saving...';
  try {
    const saved = await api(`/api/fcr/${id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(updated) });
    Object.assign(rec, saved);
    renderRow(rec);
    applyColumnVisibility();
    state.edits.delete(id); updateDirty();
    closeEditModal();
    toast('✅ Changes saved', 'success');
    loadOptions();
  } catch (err) { toast('❌ Save failed: ' + err.message, 'error'); }
  finally { btn.disabled = false; btn.innerHTML = origHTML; }
});

/* ═══ Table Rendering ═══════════════════════════════════════ */
function renderRow(rec) {
  const tr = document.querySelector(`tr[data-id="${rec.id}"]`);
  if (!tr) return;
  const tmp = document.createElement('tbody');
  tmp.innerHTML = rowHtml(rec);
  const newTr = tmp.firstElementChild;
  if (!newTr) return;
  newTr.className = tr.className;
  tr.replaceWith(newTr);
  bindRowEvents(newTr, rec.id);
  // Ensure the new row respects current column visibility
  const visible = $$('#column-menu input[type="checkbox"]:checked').map(cb => cb.dataset.col);
  newTr.querySelectorAll('td[data-col]').forEach(cell => {
    cell.classList.toggle('col-hidden', !visible.includes(cell.dataset.col));
  });
}

function rowHtml(r) {
  const sel = state.selected.has(r.id);
  const dirty = state.edits.has(r.id);
  return `<tr data-id="${r.id}" class="${sel ? 'selected' : ''} ${dirty ? 'dirty' : ''}">
    <td><input type="checkbox" class="chk" ${sel ? 'checked' : ''}></td>
    <td data-col="fcr_code" contenteditable="true" data-field="fcr_code">${escapeHtml(r.fcr_code)}</td>
    <td data-col="fcr_subject" contenteditable="true" data-field="fcr_subject" title="${escapeHtml(r.fcr_subject)}">${escapeHtml(r.fcr_subject)}</td>
    <td data-col="category" contenteditable="true" data-field="category">${escapeHtml(r.category)}</td>
    <td data-col="date" contenteditable="true" data-field="date">${escapeHtml(dateOnly(r.date))}</td>
    <td data-col="originator" contenteditable="true" data-field="originator">${escapeHtml(r.originator)}</td>
    <td data-col="action_party" contenteditable="true" data-field="action_party">${escapeHtml(r.action_party)}</td>
    <td data-col="pedec_status" class="status-cell" data-field="pedec_status" title="Double-click to edit">${pill(r.pedec_status)}</td>
    <td data-col="fcr_status" class="status-cell" data-field="fcr_status" title="Double-click to edit">${pill(r.fcr_status)}</td>
    <td data-col="execution_status" class="status-cell" data-field="execution_status" title="Double-click to edit">${pill(r.execution_status)}</td>
    <td data-col="remarks" contenteditable="true" data-field="remarks" title="${escapeHtml(r.remarks)}">${escapeHtml(r.remarks)}</td>
    <td><div class="row-actions">
      <button class="btn-edit" title="Edit all fields">✏️</button>
      <button class="btn-open" title="Open folder">📂</button>
      <button class="btn-del danger" title="Delete">🗑</button>
    </div></td>
  </tr>`;
}

function bindRowEvents(tr, id) {
  tr.querySelector('.chk').addEventListener('change', e => {
    if (e.target.checked) state.selected.add(id); else state.selected.delete(id);
    tr.classList.toggle('selected', e.target.checked);
  });
  tr.querySelectorAll('[contenteditable="true"]').forEach(cell => {
    cell.addEventListener('blur', () => {
      const f = cell.dataset.field;
      const r = state.records.find(x => x.id === id);
      if (!r) return;
      const next = cell.textContent.trim();
      // No real change → no undo entry, no dirty flag.
      if (next === (r[f] || '')) { cell.textContent = r[f] || ''; return; }
      pushUndo(id);
      r[f] = next;
      markDirty(id);
    });
    cell.addEventListener('keydown', e => {
      if (e.key === 'Enter') { e.preventDefault(); cell.blur(); }
      if (e.key === 'Escape') { cell.textContent = (state.records.find(x => x.id === id)?.[cell.dataset.field]) || ''; cell.blur(); }
    });
  });
  tr.querySelectorAll('.status-cell').forEach(cell => { cell.addEventListener('dblclick', () => openEditModal(id)); });
  tr.querySelector('.btn-edit').addEventListener('click', () => openEditModal(id));
  tr.querySelector('.btn-open').addEventListener('click', () => openFolder(id));
  tr.querySelector('.btn-del').addEventListener('click', () => deleteOne(id));
}

/* ═══ Column Visibility ═════════════════════════════════════ */
const COLUMN_PRESETS = {
  minimal: ['fcr_code', 'fcr_subject', 'pedec_status'],
  standard: ['fcr_code', 'fcr_subject', 'category', 'date', 'originator', 'action_party', 'pedec_status', 'fcr_status', 'execution_status'],
  full: ['fcr_code', 'fcr_subject', 'category', 'date', 'originator', 'action_party', 'pedec_status', 'fcr_status', 'execution_status', 'remarks']
};
function loadColumnVisibility() {
  const saved = localStorage.getItem('fcr_column_visibility');
  if (saved) {
    try {
      const visible = JSON.parse(saved);
      $$('#column-menu input[type="checkbox"]').forEach(cb => { cb.checked = visible.includes(cb.dataset.col); });
    } catch (e) {}
  }
  applyColumnVisibility();
}
function saveColumnVisibility() {
  const visible = $$('#column-menu input[type="checkbox"]:checked').map(cb => cb.dataset.col);
  localStorage.setItem('fcr_column_visibility', JSON.stringify(visible));
}
function applyColumnVisibility() {
  const visible = $$('#column-menu input[type="checkbox"]:checked').map(cb => cb.dataset.col);
  // Apply to headers and cells
  $$('#tbl-fcr thead th[data-col], #tbl-fcr tbody td[data-col]').forEach(cell => {
    const col = cell.dataset.col;
    cell.classList.toggle('col-hidden', !visible.includes(col));
  });
  // Apply to colgroup
  $$('#tbl-fcr colgroup col[data-col]').forEach(col => {
    col.classList.toggle('col-hidden', !visible.includes(col.dataset.col));
  });
}
$('#btn-columns').addEventListener('click', (e) => { e.stopPropagation(); $('#column-menu').classList.toggle('show'); });
$$('#column-menu input[type="checkbox"]').forEach(cb => {
  cb.addEventListener('change', () => { saveColumnVisibility(); applyColumnVisibility(); });
});
$('#btn-col-preset-minimal')?.addEventListener('click', () => applyPreset('minimal'));
$('#btn-col-preset-standard')?.addEventListener('click', () => applyPreset('standard'));
$('#btn-col-preset-full')?.addEventListener('click', () => applyPreset('full'));
function applyPreset(preset) {
  const cols = COLUMN_PRESETS[preset];
  $$('#column-menu input[type="checkbox"]').forEach(cb => { cb.checked = cols.includes(cb.dataset.col); });
  saveColumnVisibility(); applyColumnVisibility();
  $('#column-menu').classList.remove('show');
  toast(`Applied ${preset} view`, 'success', 1500);
}

/* ═══ Select All / Save / Discard / Delete ══════════════════ */
$('#chk-all').addEventListener('change', e => {
  const c = e.target.checked;
  state.records.forEach(r => { if (c) state.selected.add(r.id); else state.selected.delete(r.id); });
  $$('#tbody-fcr .chk').forEach(cb => { cb.checked = c; cb.closest('tr').classList.toggle('selected', c); });
});

async function saveAll() {
  if (state.edits.size === 0) { toast('No changes to save', 'warn', 1500); return; }
  try {
    const recs = [...state.edits.values()];
    const res = await api('/api/fcr/bulk-save', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ records: recs }) });
    if (res.saved) toast(`Saved ${res.saved} record(s)`, 'success');
    if (res.skipped) {
      const first = (res.errors && res.errors[0]) ? ` — ${res.errors[0]}` : '';
      toast(`${res.skipped} record(s) skipped${first}`, 'error', 7000);
    }
    if (!res.saved && !res.skipped) toast('Nothing was saved', 'warn');
    state.edits.clear();
    updateDirty();
    loadOptions();
    await loadRecords();   // re-read canonical (server-normalised) values
  } catch (e) {
    toast('Save failed: ' + errorText(e), 'error', 7000);
  }
}
$('#btn-save-all').addEventListener('click', saveAll);
$('#btn-discard').addEventListener('click', async () => {
  if (state.edits.size === 0) { toast('No changes to discard', 'warn', 1500); return; }
  const ok = await confirmDlg('Discard Changes?', `You have ${state.edits.size} unsaved change(s). Discard them?`);
  if (!ok) return;
  state.edits.clear();
  state.records = state.records.map(r => { const o = state.originals.get(r.id); return o ? { ...o } : r; });
  renderTable(); updateDirty(); toast('Changes discarded', 'info');
});
$('#btn-del-sel').addEventListener('click', async () => {
  if (state.selected.size === 0) { toast('No rows selected', 'warn', 1500); return; }
  const ok = await confirmDlg('Delete Selected?', `Permanently delete ${state.selected.size} FCR record(s)?`);
  if (!ok) return;
  try {
    const res = await api('/api/fcr/bulk-delete', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ ids: [...state.selected] }) });
    toast(`Deleted ${res.deleted} record(s)`, 'success');
    loadRecords();
  } catch (e) {}
});
async function deleteOne(id) {
  const ok = await confirmDlg('Delete FCR?', 'This record will be permanently deleted.');
  if (!ok) return;
  try { await api(`/api/fcr/${id}`, { method: 'DELETE' }); toast('Deleted', 'success'); loadRecords(); } catch (e) {}
}

/* ═══ Folder Operations ═════════════════════════════════════ */
async function openFolder(id) {
  try { const r = await api(`/api/fcr/${id}/open-folder`); if (!r.ok) toast(r.error || 'Cannot open folder', 'error'); } catch (e) {}
}

/* ═══ Export ════════════════════════════════════════════════ */
async function doExport() {
  try {
    const r = await fetch('/api/export');
    if (!r.ok) throw new Error(await r.text());
    const blob = await r.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `FCR_Export_${new Date().toISOString().slice(0, 10)}.xlsx`;
    a.click();
    URL.revokeObjectURL(url);
    toast('Export complete', 'success');
  } catch (e) { toast('Export failed', 'error'); }
}
$('#btn-export').addEventListener('click', doExport);
$('#btn-export-2')?.addEventListener('click', () => {
  window.open(API + '/api/export-with-charts', '_blank');
  toast('Downloading Excel Report with Charts...', 'success');
});

/* ═══ Add FCR ═══════════════════════════════════════════════ */
$('#form-add').addEventListener('submit', async e => {
  e.preventDefault();
  const fd = new FormData(e.target);
  const body = Object.fromEntries(fd.entries());
  try {
    await api('/api/fcr', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
    toast('FCR created', 'success');
    e.target.reset(); loadOptions();
    activatePage('records');
  } catch (err) {
    toast('❌ Could not create FCR: ' + errorText(err), 'error', 7000);
  }
});

/* ═══ Settings ══════════════════════════════════════════════ */
async function loadSettings() {
  try {
    const s = await api('/api/settings');
    state.settings = s;
    $('#set-company').value = s.company_name || '';
    $('#set-db').value = s.db_location || '';
    $('#set-root').value = s.attachment_root || '';
    $('#set-backup').value = s.backup_location || '';
    $('#set-backup-resolved').value = s.backup_resolved || '';
    $('#set-dark').checked = !!s.dark_mode;
    
    // NEW FIELDS POPULATION
    $('#set-pedec-alert-days').value = s.pedec_alert_days || 14;
    $('#set-auto-backup-enabled').checked = s.auto_backup_enabled !== false;
    $('#set-auto-backup-interval').value = s.auto_backup_interval_days || 1;
    $('#set-auto-export-enabled').checked = !!s.auto_export_enabled;
    $('#set-auto-export-interval').value = s.auto_export_interval_days || 2;
    $('#set-auto-export-location').value = s.auto_export_location || 'exports';
  } catch (e) {
    toast('Failed to load settings', 'error');
  }
}

$('#btn-save-settings').addEventListener('click', async () => {
  const payload = {
    company_name: $('#set-company').value.trim(),
    attachment_root: $('#set-root').value.trim(),
    backup_location: $('#set-backup').value.trim(),
    dark_mode: $('#set-dark').checked,
    pedec_alert_days: parseInt($('#set-pedec-alert-days').value) || 14,
    auto_backup_enabled: $('#set-auto-backup-enabled').checked,
    auto_backup_interval_days: parseInt($('#set-auto-backup-interval').value) || 1,
    auto_export_enabled: $('#set-auto-export-enabled').checked,
    auto_export_interval_days: parseInt($('#set-auto-export-interval').value) || 2,
    auto_export_location: $('#set-auto-export-location').value.trim()
  };

  try {
    const res = await api('/api/settings', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
    state.settings = res;
    applyTheme(res.dark_mode);
    refreshCharts();
    toast('✅ Settings saved successfully', 'success');
  } catch (e) {
    toast('❌ Failed to save settings: ' + errorText(e), 'error', 6000);
  }
});

/* Clean up Chart and Stat Rendering in Notification Page */
async function loadNotificationsPage() {
  const list = $('#notice-list-full');
  list.innerHTML = `<span class="text-muted">${t('loading_notif')}</span>`;
  try {
    const data = await api('/api/notifications');
    state.notifications = data.items || [];
    const count = $('#notifCount');
    if (count) count.textContent = data.count || 0;

    if (!state.notifications.length) {
      list.innerHTML = `<span class="text-muted">${t('no_overdue')}</span>`;
    } else {
      list.innerHTML = state.notifications.map(n => `
        <div class="notice-item" data-id="${n.id}" data-fcr-code="${escapeHtml(n.fcr_code || '')}">
          <strong>${escapeHtml(n.fcr_code || 'No code')} · ${n.age_days} ${t('days_overdue')}</strong>
          <span>${escapeHtml(n.fcr_subject || '')}</span>
        </div>
      `).join('');

      $$('#notice-list-full .notice-item').forEach(item => {
        item.addEventListener('click', () => goToRecords(item.dataset.fcrCode || ''));
      });
    }
  } catch (e) {
    list.innerHTML = `<span class="text-muted">Failed to load notifications</span>`;
  }
}

$('#btn-reset-settings').addEventListener('click', async () => {
  const ok = await confirmDlg('Reset Settings to Defaults?', 'This will reset all settings to factory defaults. Your database and backups will NOT be affected.\n\nThis cannot be undone.');
  if (!ok) return;
  try {
    const res = await api('/api/settings/reset', { method: 'POST' });
    if (res.ok) { toast('✅ Settings reset to defaults', 'success'); await loadSettings(); }
  } catch (e) { toast('❌ Reset failed: ' + e.message, 'error'); }
});
$('#btn-backup').addEventListener('click', async () => {
  try { const r = await api('/api/backup', { method: 'POST' }); toast(`Backup created (${r.size_kb} KB)`, 'success'); loadBackups(); } catch (e) {}
});
$('#btn-show-backups').addEventListener('click', loadBackups);
async function loadBackups() {
  try {
    const list = await api('/api/backups');
    const el = $('#backup-list');
    if (!list.length) { el.innerHTML = '<p class="text-muted">No backups yet.</p>'; return; }
    el.innerHTML = '<table class="fcr" style="font-size:12px"><thead><tr><th>Filename</th><th>Created</th><th>Size</th></tr></thead><tbody>' +
      list.map(b => `<tr><td>${escapeHtml(b.filename)}</td><td>${escapeHtml(b.created)}</td><td>${b.size_kb} KB</td></tr>`).join('') +
      '</tbody></table>';
  } catch (e) {}
}
$('#btn-reset-sort').addEventListener('click', () => {
  state.sortBy = 'date';
  state.sortOrder = 'desc';
  updateSortIndicators();
  loadRecords();
  toast('Sort reset to Date (newest first)', 'info', 1500);
});

/* ═══ Reports ═══════════════════════════════════════════════ */
async function loadReports() {
  try {
    const s = await api('/api/stats');
    $('#rp-total').textContent = s.total;
    $('#rp-open').textContent = s.open;
    $('#rp-done').textContent = s.finished;
    $('#rp-rej').textContent = s.rejected;
    drawChart('rp-pie', 'doughnut', ['Open', 'Finished', 'Pending', 'Rejected'], [s.open, s.finished, s.pending, s.rejected]);
    drawChart('rp-trend', 'bar', s.trend.map(x => x.yr), s.trend.map(x => x.cnt), '#1e3c72');
  } catch (e) {}
}

/* ═══ About ═════════════════════════════════════════════════ */
async function loadAbout() {
  try {
    const a = await api('/api/about');
    const s = await api('/api/stats');
    $('#ab-version').textContent = a.version;
    $('#ab-author').textContent = a.author;
    $('#ab-company').textContent = a.company;
    $('#ab-data-dir').textContent = a.data_dir;
    $('#ab-db').textContent = a.db_path;
    $('#ab-total').textContent = s.total;
    $('#ab-status').textContent = 'OK';
    $('#ab-schema').textContent = 'v' + a.schema_version;
    $('#ab-python').textContent = a.python;
  } catch (e) {
    toast('Could not load application info', 'error');
  }
}

/* ═══ Authentication gate ═══════════════════════════════════ */
/* Demo sign-in screen. The credentials below are a visual gate that makes the
   are only the UX gate. The real boundary is the server: /api/auth/login
   issues the HttpOnly fcr_auth cookie and every /api call is authorized
   by the backend. */
const REMEMBER_KEY = 'fcr_remember_user';

async function isAuthenticated() {
  try {
    const r = await fetch('/api/auth/me');
    return r.ok;
  } catch (e) { return false; }
}

async function signIn(username, password, remember) {
  const u = (username || '').trim();
  try {
    const r = await fetch('/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username: u, password: password || '' }),
    });
    if (!r.ok) return false;
    if (remember) localStorage.setItem(REMEMBER_KEY, u);
    else localStorage.removeItem(REMEMBER_KEY);
    return true;
  } catch (e) { return false; }
}

function hideLoginScreen() {
  const screen = $('#login-screen');
  if (!screen) return;
  screen.classList.add('hidden');
  setTimeout(() => { screen.style.display = 'none'; }, 450);
}

function showLoginForm() {
  const form = $('#login-form');
  const startup = $('#login-startup');
  if (startup) startup.classList.add('hide');
  if (form) form.classList.add('show');
  const remembered = localStorage.getItem(REMEMBER_KEY);
  if (remembered) {
    $('#login-user').value = remembered;
    $('#login-remember').checked = true;
  }
  setTimeout(() => $('#login-user').focus(), 60);
}

function loginError(message) {
  const el = $('#login-error');
  if (!el) return;
  el.textContent = message;
  el.hidden = false;
  el.classList.remove('shake');
  void el.offsetWidth;   // restart the animation
  el.classList.add('shake');
}

$('#login-eye')?.addEventListener('click', () => {
  const input = $('#login-pass');
  const show = input.type === 'password';
  input.type = show ? 'text' : 'password';
  $('#login-eye').textContent = show ? '🙈' : '👁';
  input.focus();
});

$('#login-form')?.addEventListener('submit', e => {
  e.preventDefault();
  const submit = $('#login-submit');
  const user = $('#login-user').value;
  const pass = $('#login-pass').value;
  if (!user || !pass) { loginError(t('login_missing')); return; }

  submit.disabled = true;
  // Small delay so the button feels like it verified something remotely.
  setTimeout(async () => {
    const ok = await signIn(user, pass, $('#login-remember').checked);
    submit.disabled = false;
    if (!ok) {
      loginError(t('login_invalid'));
      $('#login-pass').value = '';
      $('#login-pass').focus();
      return;
    }
    $('#login-error').hidden = true;
    // Cookie is set — reload so init() takes the authenticated path and
    // loads settings/records/charts (an unauthenticated init skips them).
    location.reload();
  }, 450);
});

/* ═══ Sortable Headers ══════════════════════════════════════ */
$$('#tbl-fcr thead th[data-sort]').forEach(th => {
  th.addEventListener('click', () => sortBy(th.dataset.sort));
});

/* ═══ Init ══════════════════════════════════════════════════ */
function setLoginProgress(width, text) {
  const bar = $('#login-progress-bar');
  if (bar) bar.style.width = width;
  const status = $('#login-status');
  if (status) status.textContent = text;
}

(async function init() {
  // Load saved language
  const savedLang = localStorage.getItem('fcr_language') || 'en';
  applyLanguage(savedLang);

  // Gate the application behind the sign-in form: only authenticated
  // sessions (valid fcr_auth cookie) load data behind the splash.
  if (!await isAuthenticated()) {
    setLoginProgress('100%', t('login_ready'));
    setTimeout(showLoginForm, 350);
    return;
  }

  try {
    setLoginProgress('25%', t('login_connecting'));
    const health = await api('/api/health');
    const versionLabel = health.version || '2.0.0';
    ['#login-version', '#sidebar-version'].forEach(sel => {
      const el = $(sel);
      if (el) el.textContent = versionLabel;
    });
    setLoginProgress('45%', t('login_loading_settings'));
    const s = await api('/api/settings');
    state.settings = s;
    applyTheme(!!s.dark_mode);
  } catch (e) {}
  setLoginProgress('62%', t('login_loading_filters'));
  await loadOptions();
  await loadDashboard();
  await loadNotifications();
  setLoginProgress('82%', t('login_loading_records'));
  await loadQuickStats();
  await loadRecords();
  updateSortIndicators();
  loadColumnVisibility();
  setLoginProgress('100%', t('login_ready'));

  // Deep link support: #records, #settings, … (bookmarkable pages)
  const requestedPage = (location.hash || '').replace(/^#(page-)?/, '');
  if (requestedPage && PAGES.includes(requestedPage)) activatePage(requestedPage, { silentHash: true });

  hideLoginScreen();
})();

/* ═══ FCR Requests (Phase 5) ═════════════════════════════════ */
const reqState = { requests: [], current: null, adminRequests: [], adminCurrent: null, reviewers: [] };

function isAdminUser() {
  const r = state.userRoles || [];
  return r.includes('FCR Admin') || r.includes('System Administrator');
}

async function loadCurrentUser() {
  try {
    const r = await fetch('/api/auth/me');
    if (!r.ok) return;
    const me = await r.json();
    state.userRoles = me.roles || [];
    const isReq = r => r === 'Requester' || r === 'System Administrator';
    const isRev = r => r === 'Reviewer' || r === 'System Administrator';
    $('#nav-requests').style.display = state.userRoles.some(isReq) ? '' : 'none';
    $('#nav-admin-requests').style.display = isAdminUser() ? '' : 'none';
    $('#nav-reviews').style.display = state.userRoles.some(isRev) ? '' : 'none';
  } catch (e) { /* nav stays hidden */ }
}

function esc(s) { return escapeHtml(String(s ?? '')); }

async function loadRequestsPage() {
  try {
    reqState.requests = await api('/api/requests');
    const tb = $('#requests-table tbody');
    tb.innerHTML = reqState.requests.map(r => `<tr data-id="${r.id}">
      <td>${esc(r.request_number)}</td><td>${esc(r.subject)}</td>
      <td>${esc(r.submission_status)}</td><td>${esc(r.submitted_at || '')}</td>
      <td>${esc(r.fcr_code || '')}</td>
      <td><button class="btn btn-sm btn-edit req-view">View</button></td></tr>`).join('');
    $$('#requests-table .req-view').forEach(b => b.addEventListener('click',
      e => showRequestDetail(e.target.closest('tr').dataset.id)));
  } catch (e) {}
}

async function showRequestDetail(id) {
  try {
    const r = await api('/api/requests/' + encodeURIComponent(id));
    reqState.current = r;
    const box = $('#request-detail');
    box.style.display = '';
    box.innerHTML = `
      <h3>${esc(r.request_number)} — ${esc(r.subject)}</h3>
      <p class="page-sub">${esc(r.submission_status)} · ${esc(r.submitted_at || '')}</p>
      <div class="form-grid mt-2">
        <div class="field full"><label>${t('req_justification').replace(' *','')}</label><p>${esc(r.justification)}</p></div>
        <div class="field full"><label>${t('req_description').replace(' *','')}</label><p>${esc(r.description)}</p></div>
        <div class="field"><label>${t('fcr_code') || 'FCR Code'}</label><p>${esc(r.fcr_code || '—')}</p></div>
        <div class="field"><label>${t('category') || 'Category'}</label><p>${esc(r.category || '—')}</p></div>
      </div>
      <h4 class="mt-3">${t('attachments')}</h4>
      <ul id="req-att-list">${r.attachments.map(a => `<li>${esc(a.original_file_name)} (${(a.file_size/1024).toFixed(1)} KB)
        <a href="/api/requests/${r.id}/attachments/${a.id}/download">${t('download')}</a></li>`).join('')}</ul>
      <form id="req-att-form" class="mt-2"><input type="file" name="f"> <button class="btn btn-sm" type="submit">${t('upload')}</button></form>
      <h4 class="mt-3">${t('comments')}</h4>
      <div id="req-comments">${r.comments.map(c => `<p><strong>${esc(c.username)}</strong> <em>${esc(c.comment_type)}</em> · ${esc(c.created_at)}<br>${esc(c.comment_text)}</p>`).join('')}</div>
      <form id="req-comment-form" class="mt-2"><input name="comment_text" required> <button class="btn btn-sm" type="submit">${t('add_comment')}</button></form>`;
    $('#req-comment-form').addEventListener('submit', async e => {
      e.preventDefault();
      await api(`/api/requests/${r.id}/comments`, { method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ comment_text: e.target.comment_text.value }) });
      showRequestDetail(id); loadRequestsPage();
    });
    $('#req-att-form').addEventListener('submit', async e => {
      e.preventDefault();
      const f = e.target.f.files[0]; if (!f) return;
      const fd = new FormData(); fd.append('file', f);
      const res = await fetch(`/api/requests/${r.id}/attachments`, { method: 'POST', body: fd });
      if (!res.ok) { toast('Upload failed: ' + await res.text(), 'error'); return; }
      showRequestDetail(id); loadRequestsPage();
    });
  } catch (e) {}
}

$('#form-request')?.addEventListener('submit', async e => {
  e.preventDefault();
  const f = e.target;
  try {
    const created = await api('/api/requests', { method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ subject: f.subject.value,
        justification: f.justification.value, description: f.description.value }) });
    for (const file of f.attachments.files) {
      const fd = new FormData(); fd.append('file', file);
      const res = await fetch(`/api/requests/${created.id}/attachments`, { method: 'POST', body: fd });
      if (!res.ok) { toast('Attachment upload failed: ' + await res.text(), 'error'); }
    }
    toast(t('req_submitted') + ': ' + created.request_number, 'success');
    f.reset(); loadRequestsPage();
  } catch (e) {}
});

const ADMIN_STATUSES = ['Draft','Submitted','Under Admin Review','Returned to Admin',
  'Under Review','Approved','Rejected','Completed'];

async function loadAdminRequestsPage() {
  const sel = $('#admin-req-status');
  if (sel.options.length <= 1)
    ADMIN_STATUSES.forEach(s => sel.insertAdjacentHTML('beforeend', `<option>${s}</option>`));
  sel.onchange = () => loadAdminRequestsPage();
  const q = sel.value ? `?status=${encodeURIComponent(sel.value)}` : '';
  try {
    reqState.adminRequests = await api('/api/admin/requests' + q);
    const tb = $('#admin-requests-table tbody');
    tb.innerHTML = reqState.adminRequests.map(r => `<tr data-id="${r.id}">
      <td>${esc(r.request_number)}</td><td>${esc(r.subject)}</td><td>${esc(r.requester_username)}</td>
      <td>${esc(r.submission_status)}</td><td>${esc(r.submitted_at || '')}</td>
      <td>${esc(r.fcr_code || '')}</td>
      <td><button class="btn btn-sm btn-edit adm-view">View</button></td></tr>`).join('');
    $$('#admin-requests-table .adm-view').forEach(b => b.addEventListener('click',
      e => showAdminRequest(e.target.closest('tr').dataset.id)));
  } catch (e) {}
}

async function showAdminRequest(id) {
  try {
    const [r, reviewers] = await Promise.all([
      api('/api/requests/' + encodeURIComponent(id)),
      api('/api/admin/requests/reviewers'),
    ]);
    reqState.adminCurrent = r; reqState.reviewers = reviewers;
    const box = $('#admin-request-detail');
    box.style.display = '';
    const wf = r.workflow || {};
    const assigned = new Set((wf.stages || []).flatMap(s => s.reviewers || [])
      .map(a => a.assigned_user_id));
    box.innerHTML = `
      <h3>${esc(r.request_number)} — ${esc(r.subject)}</h3>
      <p class="page-sub">${esc(r.submission_status)} · ${esc(r.requester_username)} · ${esc(r.submitted_at || '')}</p>
      <div class="form-grid mt-2">
        <div class="field full"><label>${t('req_justification').replace(' *','')}</label><p>${esc(r.justification)}</p></div>
        <div class="field full"><label>${t('req_description').replace(' *','')}</label><p>${esc(r.description)}</p></div>
        <div class="field"><label>FCR Code</label><input id="adm-fcr-code" value="${esc(r.fcr_code || '')}"></div>
        <div class="field"><label>${t('category') || 'Category'}</label>
          <select id="adm-category">
            <option value="">—</option>
            <option${r.category==='Permanent'?' selected':''}>Permanent</option>
            <option${r.category==='Temporary'?' selected':''}>Temporary</option>
          </select></div>
        <div class="field"><label>Linked FCR ID</label><input id="adm-linked" type="number" value="${r.linked_fcr_id ?? ''}"></div>
      </div>
      <div class="form-actions">
        <button class="btn" id="adm-save">${t('save_draft')}</button>
        <button class="btn" id="adm-create-fcr">${t('create_fcr_record')}</button>
      </div>
      <h4 class="mt-3">${t('attachments')}</h4>
      <ul>${r.attachments.map(a => `<li>${esc(a.original_file_name)} (${(a.file_size/1024).toFixed(1)} KB) — ${esc(a.uploaded_by_username)}
        <a href="/api/requests/${r.id}/attachments/${a.id}/download">${t('download')}</a>
        <button class="btn btn-sm btn-danger att-del" data-id="${a.id}">✕</button></li>`).join('')}</ul>
      <form id="adm-att-form" class="mt-2"><input type="file" name="f"> <button class="btn btn-sm" type="submit">${t('upload')}</button></form>
      <h4 class="mt-3">${t('comments')}</h4>
      <div>${r.comments.map(c => `<p><strong>${esc(c.username)}</strong> <em>${esc(c.comment_type)}</em> · ${esc(c.created_at)}<br>${esc(c.comment_text)}</p>`).join('')}</div>
      <form id="adm-comment-form" class="mt-2"><input name="comment_text" required> <button class="btn btn-sm" type="submit">${t('add_comment')}</button></form>
      <h4 class="mt-3">${t('workflow')} — ${esc(wf.workflow_status || 'Not Started')}</h4>
      <div class="form-grid">
        <div class="field"><label>${t('review_mode')}</label>
          <select id="adm-review-mode">
            <option value="sequential"${wf.review_mode==='sequential'?' selected':''}>sequential</option>
            <option value="parallel"${wf.review_mode==='parallel'?' selected':''}>parallel</option>
          </select></div>
        <div class="field"><label>${t('decision_rule')}</label>
          <select id="adm-decision-rule">
            <option value="all"${wf.stages?.[0]?.decision_rule==='all'?' selected':''}>all</option>
            <option value="any"${wf.stages?.[0]?.decision_rule==='any'?' selected':''}>any</option>
          </select></div>
        ${(wf.stages||[]).map(s => `<div class="field full"><label>${esc(s.stage_name)}
          — ${esc(s.stage_status)}</label>
          <div>${(s.reviewers||[]).map(a => `${esc(a.assigned_name)} <em>${esc(a.status)}</em>`).join(', ') || '—'}</div></div>`).join('')}
        <div class="field full"><label>${t('reviewers')}</label>
          <div id="adm-reviewer-list">${reviewers.length ? reviewers.map(u =>
            `<label><input type="checkbox" class="adm-rev" value="${u.id}"${assigned.has(u.id)?' checked':''}> ${esc(u.display_name || u.username)}</label>`
          ).join(' ') : `<em>${t('no_reviewers')}</em>`}</div></div>
      </div>
      <div class="form-actions">
        <button class="btn" id="adm-prep-wf">${t('prepare_workflow')}</button>
        <button class="btn btn-primary" id="adm-submit-review">${t('submit_for_review')}</button>
        ${r.submission_status === 'Returned to Admin' || r.submission_status === 'Rejected'
          ? `<button class="btn btn-primary" id="adm-resubmit">${t('resubmit')}</button>` : ''}
      </div>`;
    showRequestHistory(box, r.id);
    $('#adm-resubmit')?.addEventListener('click', () =>
      api(`/api/admin/requests/${r.id}/resubmit`, { method: 'POST' }).then(reload).catch(() => {}));
    const reload = () => { showAdminRequest(id); loadAdminRequestsPage(); };
    $('#adm-save').addEventListener('click', () =>
      api(`/api/admin/requests/${r.id}`, { method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ fcr_code: $('#adm-fcr-code').value,
          category: $('#adm-category').value,
          linked_fcr_id: $('#adm-linked').value ? +$('#adm-linked').value : null }) })
        .then(reload).catch(() => {}));
    $('#adm-create-fcr').addEventListener('click', () =>
      api(`/api/admin/requests/${r.id}/create-fcr`, { method: 'POST' }).then(reload).catch(() => {}));
    $('#adm-prep-wf').addEventListener('click', () =>
      api(`/api/admin/requests/${r.id}/workflow`, { method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ review_mode: $('#adm-review-mode').value,
          decision_rule: $('#adm-decision-rule').value,
          reviewer_user_ids: $$('.adm-rev:checked').map(c => +c.value) }) })
        .then(reload).catch(() => {}));
    $('#adm-submit-review').addEventListener('click', () =>
      api(`/api/admin/requests/${r.id}/submit-for-review`, { method: 'POST' }).then(reload).catch(() => {}));
    $('#adm-comment-form').addEventListener('submit', async e => {
      e.preventDefault();
      await api(`/api/requests/${r.id}/comments`, { method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ comment_text: e.target.comment_text.value }) });
      reload();
    });
    $('#adm-att-form').addEventListener('submit', async e => {
      e.preventDefault();
      const f = e.target.f.files[0]; if (!f) return;
      const fd = new FormData(); fd.append('file', f);
      const res = await fetch(`/api/requests/${r.id}/attachments`, { method: 'POST', body: fd });
      if (!res.ok) { toast('Upload failed: ' + await res.text(), 'error'); return; }
      reload();
    });
    $$('.att-del').forEach(b => b.addEventListener('click', () =>
      api(`/api/requests/${r.id}/attachments/${b.dataset.id}`, { method: 'DELETE' }).then(reload).catch(() => {})));
  } catch (e) {}
}

/* ── My Reviews page (Phase 6) ─────────────────────────────── */
async function loadReviewsPage() {
  try {
    const tasks = await api('/api/reviews/mine');
    const tb = $('#reviews-table tbody');
    tb.innerHTML = tasks.map(t => `<tr data-id="${t.request_id}">
      <td>${esc(t.request_number)}</td><td>${esc(t.subject)}</td>
      <td>${esc(t.stage_name)}</td><td>${t.cycle}</td>
      <td>${esc(t.request_status)}</td>
      <td><button class="btn btn-sm btn-edit rev-open">${t('review')}</button></td></tr>`).join('');
    $$('#reviews-table .rev-open').forEach(b => b.addEventListener('click',
      e => showReviewTask(e.target.closest('tr').dataset.id)));
    loadWorkflowNotifications();
  } catch (e) {}
}

async function loadWorkflowNotifications() {
  try {
    const n = await api('/api/my/notifications');
    if (!n.items.length) return;
    const box = $('#review-detail');
    if (box.style.display === 'none' && n.unread > 0)
      toast(`${n.unread} ${t('notifications_wf')}`, 'info');
  } catch (e) {}
}

async function showReviewTask(requestId) {
  try {
    const [r, history] = await Promise.all([
      api('/api/requests/' + encodeURIComponent(requestId)),
      api(`/api/requests/${encodeURIComponent(requestId)}/history`),
    ]);
    const box = $('#review-detail');
    box.style.display = '';
    box.innerHTML = `
      <h3>${esc(r.request_number)} — ${esc(r.subject)}</h3>
      <p class="page-sub">${esc(r.submission_status)} · ${esc(r.requester_username)}</p>
      <div class="form-grid mt-2">
        <div class="field full"><label>${t('req_justification').replace(' *','')}</label><p>${esc(r.justification)}</p></div>
        <div class="field full"><label>${t('req_description').replace(' *','')}</label><p>${esc(r.description)}</p></div>
      </div>
      <h4 class="mt-3">${t('attachments')}</h4>
      <ul>${r.attachments.map(a => `<li>${esc(a.original_file_name)}
        <a href="/api/requests/${r.id}/attachments/${a.id}/download">${t('download')}</a></li>`).join('')}</ul>
      <form id="rev-decision" class="mt-3">
        <div class="form-grid">
          <div class="field full"><label>${t('comment_optional')}</label>
            <textarea name="comment"></textarea></div>
        </div>
        <div class="form-actions">
          <button class="btn btn-success" name="act" value="approve">${t('approve')}</button>
          <button class="btn" name="act" value="comment">${t('add_comment')}</button>
          <button class="btn" name="act" value="return">${t('return_for_correction')}</button>
          <button class="btn btn-danger" name="act" value="reject">${t('reject')}</button>
        </div>
      </form>
      <h4 class="mt-3">${t('review_history')}</h4>
      <div>${history.map(h => `<p><strong>${esc(h.reviewer_name)}</strong> · ${esc(h.action_type)}
        · ${esc(h.stage_name)} · cycle ${h.review_cycle} · ${esc(h.created_at)}
        <br><em>${esc(h.previous_status)} → ${esc(h.resulting_status)}</em>${h.comment ? '<br>' + esc(h.comment) : ''}</p>`).join('')
        || '<em>—</em>'}</div>`;
    $('#rev-decision').addEventListener('submit', async e => {
      e.preventDefault();
      const act = e.submitter?.value || 'comment';
      try {
        await api(`/api/reviews/${encodeURIComponent(requestId)}/decision`, {
          method: 'POST', headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ action: act, comment: e.target.comment.value || null }) });
        toast(t('req_submitted'), 'success');
        box.style.display = 'none';
        loadReviewsPage();
      } catch (err) {}
    });
  } catch (e) {}
}

/* ── admin resubmit + history on the queue page ────────────── */
async function showRequestHistory(box, requestId) {
  try {
    const history = await api(`/api/requests/${encodeURIComponent(requestId)}/history`);
    const div = document.createElement('div');
    div.innerHTML = `<h4 class="mt-3">${t('review_history')}</h4>` +
      (history.map(h => `<p><strong>${esc(h.reviewer_name)}</strong> · ${esc(h.action_type)}
        · ${esc(h.stage_name)} · cycle ${h.review_cycle} · ${esc(h.created_at)}
        <br><em>${esc(h.previous_status)} → ${esc(h.resulting_status)}</em>${h.comment ? '<br>' + esc(h.comment) : ''}</p>`).join('')
        || '<em>—</em>');
    box.appendChild(div);
  } catch (e) {}
}

loadCurrentUser();
})();
