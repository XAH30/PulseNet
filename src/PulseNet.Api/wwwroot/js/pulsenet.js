(() => {
  'use strict';

  const STORAGE_TOKEN_KEY = 'pulsenet_bearer_token';
  const STORAGE_USER_ID_KEY = 'pulsenet_user_id';
  const DEFAULT_PROFILE_ID = '52648001-f863-4140-aa36-2f7ea736a4eb';

  const spotlightBg = document.getElementById('spotlightBg');
  const constellationCanvas = document.getElementById('constellationCanvas');
  const ripplesContainer = document.getElementById('ripplesContainer');
  const cloudSvg = document.getElementById('cloudSvg');
  const cloudMorphPath = document.getElementById('cloudMorphPath');
  const contentContainer = document.getElementById('contentContainer');

  // Panda Mascot
  const pandaMascot = document.getElementById('pandaMascot');
  const pandaPupilLeft = document.getElementById('pandaPupilLeft');
  const pandaPupilRight = document.getElementById('pandaPupilRight');
  const pandaMouthPath = document.getElementById('pandaMouthPath');

  // Panels
  const panelLogin = document.getElementById('panelLogin');
  const panelRegister = document.getElementById('panelRegister');
  const panelProfile = document.getElementById('panelProfile');

  // Login Form
  const loginForm = document.getElementById('loginForm');
  const loginUserIdInput = document.getElementById('loginUserId');
  const loginPasswordInput = document.getElementById('loginPassword');
  const toggleLoginEye = document.getElementById('toggleLoginEye');
  const loginFeedback = document.getElementById('loginFeedback');
  const quickCreateFromOptions = document.getElementById('quickCreateFromOptions');

  // Register Form
  const registerForm = document.getElementById('registerForm');
  const regFirstName = document.getElementById('regFirstName');
  const regSecondName = document.getElementById('regSecondName');
  const regBirthdate = document.getElementById('regBirthdate');
  const regCity = document.getElementById('regCity');
  const regBiography = document.getElementById('regBiography');
  const regPassword = document.getElementById('regPassword');
  const toggleRegEye = document.getElementById('toggleRegEye');
  const registerFeedback = document.getElementById('registerFeedback');

  // Segmented Gender
  const genderSegmented = document.getElementById('genderSegmented');
  const genderButtons = Array.from(genderSegmented.querySelectorAll('.gender-btn'));
  let selectedGender = 'male';

  // Profile Panel
  const profileEmptyState = document.getElementById('profileEmptyState');
  const emptyStateTitle = document.getElementById('emptyStateTitle');
  const emptyStateReason = document.getElementById('emptyStateReason');
  const profileCard = document.getElementById('profileCard');
  const profileAvatar = document.getElementById('profileAvatar');
  const profileCityBadge = document.getElementById('profileCityBadge');
  const profileGenderBadge = document.getElementById('profileGenderBadge');
  const profileAgeBadge = document.getElementById('profileAgeBadge');
  const profileFullName = document.getElementById('profileFullName');
  const profileIdValue = document.getElementById('profileIdValue');
  const profileFirstName = document.getElementById('profileFirstName');
  const profileSecondName = document.getElementById('profileSecondName');
  const profileBirthdate = document.getElementById('profileBirthdate');
  const profileGenderText = document.getElementById('profileGenderText');
  const profileBiography = document.getElementById('profileBiography');
  const copyIdBtn = document.getElementById('copyIdBtn');
  const copyIdLabel = document.getElementById('copyIdLabel');
  const useIdForLoginBtn = document.getElementById('useIdForLoginBtn');

  // Right Typewriter
  const typewriterTitle = document.getElementById('typewriterTitle');
  const typewriterDesc = document.getElementById('typewriterDesc');

  // Top Session Chip
  const sessionDot = document.getElementById('sessionDot');
  const sessionText = document.getElementById('sessionText');
  const logoutBtn = document.getElementById('logoutBtn');
  const authActiveBadge = document.getElementById('authActiveBadge');

  // Apple Dock & Search Popover above Search button
  const appleDock = document.getElementById('appleDock');
  const dockButtons = Array.from(appleDock.querySelectorAll('.dock-btn[data-dock]'));
  const dockSearchAnchor = document.getElementById('dockSearchAnchor');
  const dockSearchTrigger = document.getElementById('dockSearchTrigger');
  const dockSearchForm = document.getElementById('dockSearchForm');
  const dockSearchInput = document.getElementById('dockSearchInput');
  const dockSearchClose = document.getElementById('dockSearchClose');
  const dockHeartBtn = document.getElementById('dockHeartBtn');
  const heartParticlesAnchor = document.getElementById('heartParticlesAnchor');

  let currentViewedProfileId = DEFAULT_PROFILE_ID;
  let isTransitioning = false;
  let isPasswordFocused = false;
  let isLoginPasswordVisible = false;
  let isRegPasswordVisible = false;
  let isSearchPopoverOpen = false;
  let happyTimeoutId = null;
  let copyTimerId = null;

  // ===================== 1. MOUSE TRACKING, SPOTLIGHT & PANDA PUPILS =====================
  let targetMouseX = window.innerWidth / 2;
  let targetMouseY = window.innerHeight / 2;
  let smoothMouseX = targetMouseX;
  let smoothMouseY = targetMouseY;

  window.addEventListener('mousemove', (e) => {
    targetMouseX = e.clientX;
    targetMouseY = e.clientY;

    const cx = window.innerWidth / 2 - 220;
    const cy = window.innerHeight / 2 - 140;
    const px = Math.max(-4.5, Math.min(4.5, ((e.clientX - cx) / 300) * 4.5));
    const py = Math.max(-4.5, Math.min(4.5, ((e.clientY - cy) / 200) * 4.5));
    const pupilTransform = `translate(${px.toFixed(2)}px, ${py.toFixed(2)}px)`;
    pandaPupilLeft.style.transform = pupilTransform;
    pandaPupilRight.style.transform = pupilTransform;
  });

  // ===================== 2. INTERACTIVE CONSTELLATION CANVAS =====================
  function initConstellation() {
    const ctx = constellationCanvas.getContext('2d');
    const particles = [];
    const numParticles = 35;

    function resize() {
      constellationCanvas.width = window.innerWidth;
      constellationCanvas.height = window.innerHeight;
    }
    window.addEventListener('resize', resize);
    resize();

    for (let i = 0; i < numParticles; i++) {
      particles.push({
        x: Math.random() * constellationCanvas.width,
        y: Math.random() * constellationCanvas.height,
        vx: (Math.random() - 0.5) * 0.75,
        vy: (Math.random() - 0.5) * 0.75,
        radius: Math.random() * 2 + 1
      });
    }

    function frame() {
      smoothMouseX += (targetMouseX - smoothMouseX) * 0.08;
      smoothMouseY += (targetMouseY - smoothMouseY) * 0.08;

      spotlightBg.style.background = `radial-gradient(1100px circle at ${smoothMouseX.toFixed(0)}px ${smoothMouseY.toFixed(0)}px, #7c3aed 0%, #3b0764 52%, #0f172a 95%)`;

      ctx.clearRect(0, 0, constellationCanvas.width, constellationCanvas.height);

      for (let i = 0; i < particles.length; i++) {
        const p = particles[i];
        p.x += p.vx;
        p.y += p.vy;

        if (p.x < 0 || p.x > constellationCanvas.width) p.vx *= -1;
        if (p.y < 0 || p.y > constellationCanvas.height) p.vy *= -1;

        ctx.beginPath();
        ctx.arc(p.x, p.y, p.radius, 0, Math.PI * 2);
        ctx.fillStyle = 'rgba(255, 255, 255, 0.6)';
        ctx.fill();

        for (let j = i + 1; j < particles.length; j++) {
          const p2 = particles[j];
          const dx = p.x - p2.x;
          const dy = p.y - p2.y;
          const dist = Math.sqrt(dx * dx + dy * dy);
          if (dist < 120) {
            ctx.beginPath();
            ctx.moveTo(p.x, p.y);
            ctx.lineTo(p2.x, p2.y);
            ctx.strokeStyle = `rgba(255, 255, 255, ${(1 - dist / 120) * 0.35})`;
            ctx.lineWidth = 1;
            ctx.stroke();
          }
        }

        const mdx = p.x - smoothMouseX;
        const mdy = p.y - smoothMouseY;
        const mdist = Math.sqrt(mdx * mdx + mdy * mdy);
        if (mdist < 180) {
          ctx.beginPath();
          ctx.moveTo(p.x, p.y);
          ctx.lineTo(smoothMouseX, smoothMouseY);
          ctx.strokeStyle = `rgba(253, 224, 71, ${(1 - mdist / 180) * 0.65})`;
          ctx.lineWidth = 1.4;
          ctx.stroke();
        }
      }

      requestAnimationFrame(frame);
    }

    requestAnimationFrame(frame);
  }

  // ===================== 3. ORGANIC CLOUD PATH MORPHING =====================
  const cloudShapeA = [350, 0, 420, -10, 460, 30, 520, 20, 570, 60, 640, 50, 710, 100, 800, 90, 870, 150, 960, 140, 1000, 220, 1000, 380, 980, 440, 920, 450, 900, 520, 830, 500, 810, 560, 730, 530, 700, 590, 620, 560, 580, 610, 500, 570, 450, 610, 380, 570, 330, 610, 260, 550, 210, 580, 140, 510, 70, 540, 0, 460];
  const cloudShapeB = [350, 0, 435, -25, 460, 30, 505, 35, 570, 60, 655, 35, 710, 100, 785, 105, 870, 150, 975, 125, 1000, 220, 1000, 380, 995, 425, 920, 450, 885, 535, 830, 500, 825, 545, 730, 530, 685, 605, 620, 560, 595, 595, 500, 570, 435, 625, 380, 570, 345, 595, 260, 550, 195, 595, 140, 510, 85, 525, 0, 460];

  function buildCloudD(nums) {
    return `M 0 0 L ${nums[0]} ${nums[1]} Q ${nums[2]} ${nums[3]} ${nums[4]} ${nums[5]} Q ${nums[6]} ${nums[7]} ${nums[8]} ${nums[9]} Q ${nums[10]} ${nums[11]} ${nums[12]} ${nums[13]} Q ${nums[14]} ${nums[15]} ${nums[16]} ${nums[17]} Q ${nums[18]} ${nums[19]} ${nums[20]} ${nums[21]} L ${nums[22]} ${nums[23]} Q ${nums[24]} ${nums[25]} ${nums[26]} ${nums[27]} Q ${nums[28]} ${nums[29]} ${nums[30]} ${nums[31]} Q ${nums[32]} ${nums[33]} ${nums[34]} ${nums[35]} Q ${nums[36]} ${nums[37]} ${nums[38]} ${nums[39]} Q ${nums[40]} ${nums[41]} ${nums[42]} ${nums[43]} Q ${nums[44]} ${nums[45]} ${nums[46]} ${nums[47]} Q ${nums[48]} ${nums[49]} ${nums[50]} ${nums[51]} Q ${nums[52]} ${nums[53]} ${nums[54]} ${nums[55]} Q ${nums[56]} ${nums[57]} ${nums[58]} ${nums[59]} Z`;
  }

  function startCloudMorphLoop() {
    const startTime = performance.now();
    function morphTick(now) {
      const t = (Math.sin(((now - startTime) / 8000) * Math.PI * 2 - Math.PI / 2) + 1) / 2;
      const lerped = cloudShapeA.map((a, idx) => (a + (cloudShapeB[idx] - a) * t).toFixed(1));
      cloudMorphPath.setAttribute('d', buildCloudD(lerped));
      requestAnimationFrame(morphTick);
    }
    requestAnimationFrame(morphTick);
  }

  // ===================== 4. PANDA EMOTIONS & EYE-COVERING =====================
  function updatePandaCoverState() {
    const activePanelIsLogin = panelLogin.classList.contains('active');
    const isVisible = activePanelIsLogin ? isLoginPasswordVisible : isRegPasswordVisible;
    const shouldCover = isPasswordFocused && !isVisible;
    pandaMascot.classList.toggle('is-covering', shouldCover);
  }

  function triggerPandaHappy(durationMs = 1400) {
    pandaMascot.classList.add('is-happy');
    pandaMouthPath.setAttribute('d', 'M 42 67 Q 50 82 58 67');
    pandaMouthPath.setAttribute('fill', '#231f32');
    if (happyTimeoutId) clearTimeout(happyTimeoutId);
    happyTimeoutId = setTimeout(() => {
      pandaMascot.classList.remove('is-happy');
      pandaMouthPath.setAttribute('d', 'M 45 70 Q 50 74 55 70');
      pandaMouthPath.setAttribute('fill', 'transparent');
    }, durationMs);
  }

  pandaMascot.addEventListener('mousedown', () => triggerPandaHappy(1600));
  pandaMascot.addEventListener('touchstart', () => triggerPandaHappy(1600), { passive: true });

  [loginPasswordInput, regPassword].forEach((inp) => {
    inp.addEventListener('focus', () => {
      isPasswordFocused = true;
      updatePandaCoverState();
    });
    inp.addEventListener('blur', () => {
      isPasswordFocused = false;
      updatePandaCoverState();
    });
  });

  toggleLoginEye.addEventListener('mousedown', (e) => e.preventDefault());
  toggleLoginEye.addEventListener('click', () => {
    isLoginPasswordVisible = !isLoginPasswordVisible;
    loginPasswordInput.type = isLoginPasswordVisible ? 'text' : 'password';
    updatePandaCoverState();
  });

  toggleRegEye.addEventListener('mousedown', (e) => e.preventDefault());
  toggleRegEye.addEventListener('click', () => {
    isRegPasswordVisible = !isRegPasswordVisible;
    regPassword.type = isRegPasswordVisible ? 'text' : 'password';
    updatePandaCoverState();
  });

  // ===================== 5. TYPING RIPPLES =====================
  function spawnTypingRipple(e) {
    if (e.key && (e.key.length === 1 || e.key === 'Backspace')) {
      const isTopRight = Math.random() > 0.5;
      const x = isTopRight ? 62 + Math.random() * 32 : 8 + Math.random() * 30;
      const y = isTopRight ? 10 + Math.random() * 35 : 62 + Math.random() * 28;
      const ripple = document.createElement('div');
      ripple.className = 'typing-ripple';
      ripple.style.left = `calc(${x}% - 50px)`;
      ripple.style.top = `calc(${y}% - 50px)`;
      ripplesContainer.appendChild(ripple);
      while (ripplesContainer.children.length > 4) {
        ripplesContainer.removeChild(ripplesContainer.firstChild);
      }
      setTimeout(() => ripple.remove(), 1250);
    }
  }

  document.querySelectorAll('.input-field').forEach((input) => {
    input.addEventListener('keydown', spawnTypingRipple);
  });

  // ===================== 6. COMMERCIAL TYPEWRITER WITH GOLD HOVER =====================
  function renderTypewriter(container, lines, baseDelaySec) {
    container.textContent = '';
    let charCounter = 0;
    lines.forEach((line, lineIdx) => {
      for (const ch of line) {
        const span = document.createElement('span');
        span.className = 'tw-char';
        span.textContent = ch === ' ' ? '\u00A0' : ch;
        span.style.animationDelay = `${(baseDelaySec + charCounter * 0.018).toFixed(3)}s`;
        container.appendChild(span);
        charCounter++;
      }
      if (lineIdx < lines.length - 1) {
        container.appendChild(document.createElement('br'));
      }
    });
  }

  function updateRightCopy(mode) {
    if (mode === 'login') {
      renderTypewriter(typewriterTitle, ['Рады видеть вас', 'снова!'], 0.15);
      renderTypewriter(
        typewriterDesc,
        [
          'Общайтесь с единомышленниками,',
          'делитесь профессиональными интересами',
          'и находите людей в своём городе.'
        ],
        0.35
      );
    } else if (mode === 'register') {
      renderTypewriter(typewriterTitle, ['Присоединяйтесь', 'к PulseNet'], 0.15);
      renderTypewriter(
        typewriterDesc,
        [
          'Заполните короткую анкету участника',
          'и станьте частью открытого сообщества',
          'уже сегодня.'
        ],
        0.35
      );
    } else {
      renderTypewriter(typewriterTitle, ['Сообщество', 'PulseNet'], 0.15);
      renderTypewriter(
        typewriterDesc,
        [
          'Открытые профили участников,',
          'общие увлечения, города и живые',
          'отклики в один клик.'
        ],
        0.35
      );
    }
  }

  // ===================== 7. APPLE DOCK & SEARCH POPOVER ABOVE BUTTON =====================
  function getButtonLeftRelativeToDock(btn) {
    let el = btn;
    let left = 0;
    while (el && el !== appleDock) {
      left += el.offsetLeft;
      el = el.offsetParent;
    }
    return left;
  }

  function updateDockPill(dockKey) {
    dockButtons.forEach((btn) => {
      btn.classList.toggle('active', btn.dataset.dock === dockKey);
    });
    const activeBtn = dockButtons.find((b) => b.dataset.dock === dockKey) || dockButtons[0];
    if (activeBtn) {
      const left = getButtonLeftRelativeToDock(activeBtn);
      appleDock.style.setProperty('--dock-x', String(left));
      appleDock.style.setProperty('--dock-w', String(activeBtn.offsetWidth));
    }
  }

  function setSearchPopoverOpen(open) {
    isSearchPopoverOpen = open;
    dockSearchForm.classList.toggle('open', open);
    if (open) {
      dockSearchInput.value = currentViewedProfileId || '';
      updateDockPill('search');
      setTimeout(() => dockSearchInput.focus(), 60);
    }
  }

  function triggerHeartBurst() {
    triggerPandaHappy(1600);
    const configs = [
      { x: -18, delay: 0, scale: 0.88, stops: ['#f59e0b', '#ec4899', '#8b5cf6'] },
      { x: 2, delay: 80, scale: 1.18, stops: ['#fbbf24', '#f472b6', '#7c3aed'] },
      { x: 20, delay: 160, scale: 0.96, stops: ['#f97316', '#db2777', '#4f46e5'] }
    ];

    configs.forEach((cfg) => {
      setTimeout(() => {
        const wrapper = document.createElement('div');
        wrapper.className = 'floating-heart-particle';
        wrapper.style.setProperty('--hx', String(cfg.x));
        wrapper.style.setProperty('--hscale', String(cfg.scale));

        const gradId = `hg-${Date.now()}-${Math.floor(Math.random() * 10000)}`;
        const svgNS = 'http://www.w3.org/2000/svg';
        const svg = document.createElementNS(svgNS, 'svg');
        svg.setAttribute('width', '22');
        svg.setAttribute('height', '22');
        svg.setAttribute('viewBox', '0 0 24 24');

        const defs = document.createElementNS(svgNS, 'defs');
        const lg = document.createElementNS(svgNS, 'linearGradient');
        lg.setAttribute('id', gradId);
        lg.setAttribute('x1', '0%');
        lg.setAttribute('y1', '100%');
        lg.setAttribute('x2', '100%');
        lg.setAttribute('y2', '0%');

        ['0%', '50%', '100%'].forEach((offset, i) => {
          const stop = document.createElementNS(svgNS, 'stop');
          stop.setAttribute('offset', offset);
          stop.setAttribute('stop-color', cfg.stops[i]);
          lg.appendChild(stop);
        });

        defs.appendChild(lg);
        svg.appendChild(defs);

        const path = document.createElementNS(svgNS, 'path');
        path.setAttribute(
          'd',
          'M12 21.35L10.55 20.03C5.4 15.36 2 12.28 2 8.5C2 5.42 4.42 3 7.5 3C9.24 3 10.91 3.81 12 5.09C13.09 3.81 14.76 3 16.5 3C19.58 3 22 5.42 22 8.5C22 12.28 18.6 15.36 13.45 20.04L12 21.35Z'
        );
        path.setAttribute('fill', `url(#${gradId})`);
        svg.appendChild(path);

        wrapper.appendChild(svg);
        heartParticlesAnchor.appendChild(wrapper);
        setTimeout(() => wrapper.remove(), 1200);
      }, cfg.delay);
    });
  }

  dockHeartBtn.addEventListener('click', triggerHeartBurst);

  // ===================== 8. CLOUD TRANSITION BETWEEN PANELS =====================
  function switchCloudPanel(targetMode, afterSwitch) {
    if (isTransitioning) return;
    isTransitioning = true;
    setSearchPopoverOpen(false);

    cloudSvg.classList.add('closing');
    contentContainer.classList.add('transitioning');

    setTimeout(() => {
      panelLogin.classList.toggle('active', targetMode === 'login');
      panelRegister.classList.toggle('active', targetMode === 'register');
      panelProfile.classList.toggle('active', targetMode === 'profile' || targetMode === 'me');

      updateRightCopy(targetMode);
      updateDockPill(targetMode);
      if (targetMode === 'register') {
        requestAnimationFrame(updateGenderIndicator);
      }
      if (typeof afterSwitch === 'function') {
        afterSwitch();
      }

      cloudSvg.classList.remove('closing');
      setTimeout(() => {
        contentContainer.classList.remove('transitioning');
        isTransitioning = false;
      }, 140);
    }, 340);
  }

  // ===================== 9. GENDER SEGMENTED CONTROL =====================
  function updateGenderIndicator() {
    const activeBtn = genderButtons.find((b) => b.dataset.gender === selectedGender) || genderButtons[0];
    if (!activeBtn) return;
    genderSegmented.style.setProperty('--seg-x', String(activeBtn.offsetLeft));
    genderSegmented.style.setProperty('--seg-w', String(activeBtn.offsetWidth));
  }

  genderButtons.forEach((btn) => {
    btn.addEventListener('click', () => {
      selectedGender = btn.dataset.gender || 'unspecified';
      genderButtons.forEach((b) => {
        const isCurrent = b === btn;
        b.classList.toggle('active', isCurrent);
        b.setAttribute('aria-checked', isCurrent ? 'true' : 'false');
      });
      updateGenderIndicator();
    });
  });

  // ===================== 10. SESSION & API LOGIC =====================
  function getToken() {
    return sessionStorage.getItem(STORAGE_TOKEN_KEY);
  }

  function getSessionUserId() {
    return sessionStorage.getItem(STORAGE_USER_ID_KEY);
  }

  function setSession(userId, token) {
    sessionStorage.setItem(STORAGE_USER_ID_KEY, userId);
    sessionStorage.setItem(STORAGE_TOKEN_KEY, token);
    renderSessionHeader();
  }

  function clearSession() {
    sessionStorage.removeItem(STORAGE_USER_ID_KEY);
    sessionStorage.removeItem(STORAGE_TOKEN_KEY);
    renderSessionHeader();
  }

  function renderSessionHeader() {
    const token = getToken();
    const userId = getSessionUserId();
    if (token && userId) {
      sessionDot.classList.add('online');
      sessionText.textContent = `В сети · ${userId.slice(0, 8)}…`;
      logoutBtn.classList.remove('hidden');
      authActiveBadge.classList.remove('hidden');
    } else {
      sessionDot.classList.remove('online');
      sessionText.textContent = 'Гостевой режим';
      logoutBtn.classList.add('hidden');
      authActiveBadge.classList.add('hidden');
    }
  }

  logoutBtn.addEventListener('click', () => {
    clearSession();
    switchCloudPanel('login');
  });

  function showFeedback(el, message) {
    el.textContent = message;
    el.classList.remove('hidden');
  }

  function hideFeedback(el) {
    el.classList.add('hidden');
    el.textContent = '';
  }

  function formatGenderLabel(gender) {
    switch ((gender || '').toLowerCase()) {
      case 'male': return 'Мужской';
      case 'female': return 'Женский';
      default: return 'Не указан';
    }
  }

  function calculateAgeText(birthdateStr) {
    const parts = (birthdateStr || '').split('-');
    if (parts.length !== 3) return birthdateStr;
    const birthDate = new Date(Number(parts[0]), Number(parts[1]) - 1, Number(parts[2]));
    if (Number.isNaN(birthDate.getTime())) return birthdateStr;
    const now = new Date();
    let age = now.getFullYear() - birthDate.getFullYear();
    const m = now.getMonth() - birthDate.getMonth();
    if (m < 0 || (m === 0 && now.getDate() < birthDate.getDate())) {
      age--;
    }
    return age >= 0 ? `${age} лет` : birthdateStr;
  }

  function renderProfileEmpty(title, reason) {
    profileCard.classList.add('hidden');
    profileEmptyState.classList.remove('hidden');
    emptyStateTitle.textContent = title;
    emptyStateReason.textContent = reason;
  }

  function renderProfileData(user) {
    profileEmptyState.classList.add('hidden');
    profileCard.classList.remove('hidden');

    currentViewedProfileId = user.id;
    const initials = `${(user.first_name || 'P')[0] || 'P'}${(user.second_name || 'N')[0] || 'N'}`.toUpperCase();
    profileAvatar.textContent = initials;
    profileCityBadge.textContent = user.city || '—';
    profileGenderBadge.textContent = formatGenderLabel(user.gender);
    profileAgeBadge.textContent = calculateAgeText(user.birthdate);
    profileFullName.textContent = `${user.first_name} ${user.second_name}`;
    profileIdValue.textContent = user.id;
    profileFirstName.textContent = user.first_name;
    profileSecondName.textContent = user.second_name;
    profileBirthdate.textContent = user.birthdate;
    profileGenderText.textContent = formatGenderLabel(user.gender);
    profileBiography.textContent = user.biography;
  }

  async function loadProfileById(userId) {
    const targetId = (userId || '').trim();
    switchCloudPanel('profile', async () => {
      if (!targetId) {
        renderProfileEmpty('Профиль не выбран', 'Укажите ID анкеты в поиске или зарегистрируйте новый профиль.');
        return;
      }

      try {
        const response = await fetch(`/user/get/${encodeURIComponent(targetId)}`, {
          headers: { 'Accept': 'application/json' }
        });

        if (response.status === 404) {
          renderProfileEmpty('Анкета не найдена', 'Пользователь с указанным ID не зарегистрирован.');
          return;
        }

        if (!response.ok) {
          renderProfileEmpty('Некорректный ID', 'Проверьте правильность введённого идентификатора анкеты.');
          return;
        }

        const user = await response.json();
        history.replaceState(null, '', `/profile/${encodeURIComponent(user.id)}`);
        renderProfileData(user);
        triggerPandaHappy(1400);
      } catch {
        renderProfileEmpty('Ошибка соединения', 'Не удалось загрузить анкету.');
      }
    });
  }

  async function loadMyProfile() {
    const token = getToken();
    switchCloudPanel('me', async () => {
      if (!token) {
        renderProfileEmpty(
          'Вы ещё не вошли в аккаунт',
          'Авторизуйтесь или создайте новую анкету, чтобы открыть свой личный профиль.'
        );
        return;
      }

      try {
        const response = await fetch('/user/me', {
          headers: {
            'Accept': 'application/json',
            'Authorization': `Bearer ${token}`
          }
        });

        if (!response.ok) {
          clearSession();
          renderProfileEmpty('Сессия завершена', 'Пожалуйста, войдите в аккаунт повторно.');
          return;
        }

        const user = await response.json();
        renderProfileData(user);
        triggerHeartBurst();
      } catch {
        renderProfileEmpty('Ошибка сети', 'Не удалось загрузить профиль.');
      }
    });
  }

  loginForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    hideFeedback(loginFeedback);

    const id = loginUserIdInput.value.trim();
    const password = loginPasswordInput.value;

    try {
      const response = await fetch('/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
        body: JSON.stringify({ id, password })
      });

      const body = await response.json().catch(() => ({}));
      if (response.status === 404) {
        showFeedback(loginFeedback, 'Аккаунт с таким ID не найден.');
        return;
      }

      if (!response.ok || !body.token) {
        showFeedback(loginFeedback, body.message || 'Неверный ID профиля или пароль.');
        return;
      }

      setSession(id, body.token);
      triggerHeartBurst();
      await loadProfileById(id);
    } catch {
      showFeedback(loginFeedback, 'Не удалось выполнить вход. Проверьте подключение.');
    }
  });

  registerForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    hideFeedback(registerFeedback);

    const payload = {
      first_name: regFirstName.value.trim(),
      second_name: regSecondName.value.trim(),
      birthdate: regBirthdate.value,
      gender: selectedGender,
      biography: regBiography.value.trim(),
      city: regCity.value.trim(),
      password: regPassword.value
    };

    try {
      const response = await fetch('/user/register', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
        body: JSON.stringify(payload)
      });

      const body = await response.json().catch(() => ({}));
      if (!response.ok || !body.user_id) {
        showFeedback(registerFeedback, body.message || 'Пожалуйста, заполните все поля анкеты.');
        return;
      }

      const createdId = body.user_id;
      loginUserIdInput.value = createdId;
      loginPasswordInput.value = payload.password;

      const loginResp = await fetch('/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
        body: JSON.stringify({ id: createdId, password: payload.password })
      });

      if (loginResp.ok) {
        const loginData = await loginResp.json();
        if (loginData.token) {
          setSession(createdId, loginData.token);
        }
      }

      triggerHeartBurst();
      await loadProfileById(createdId);
    } catch {
      showFeedback(registerFeedback, 'Не удалось сохранить анкету.');
    }
  });

  // ===================== 11. NAVIGATION & POPOVER EVENTS =====================
  document.getElementById('goToRegisterLink').addEventListener('click', (e) => {
    e.preventDefault();
    history.replaceState(null, '', '/');
    switchCloudPanel('register');
  });

  quickCreateFromOptions.addEventListener('click', (e) => {
    e.preventDefault();
    history.replaceState(null, '', '/');
    switchCloudPanel('register');
  });

  document.getElementById('goToLoginLink').addEventListener('click', (e) => {
    e.preventDefault();
    history.replaceState(null, '', '/');
    switchCloudPanel('login');
  });

  document.getElementById('brandHomeBtn').addEventListener('click', () => {
    history.replaceState(null, '', '/');
    switchCloudPanel('login');
  });

  document.getElementById('emptyGoCreateBtn').addEventListener('click', () => switchCloudPanel('register'));

  useIdForLoginBtn.addEventListener('click', () => {
    if (currentViewedProfileId) {
      loginUserIdInput.value = currentViewedProfileId;
    }
    switchCloudPanel('login');
  });

  copyIdBtn.addEventListener('click', async () => {
    if (!currentViewedProfileId) return;
    try {
      await navigator.clipboard.writeText(currentViewedProfileId);
    } catch {
      // ignore
    }
    copyIdBtn.classList.add('is-copied');
    copyIdLabel.textContent = 'Скопировано';
    triggerPandaHappy(1000);
    if (copyTimerId) clearTimeout(copyTimerId);
    copyTimerId = setTimeout(() => {
      copyIdBtn.classList.remove('is-copied');
      copyIdLabel.textContent = 'Скопировать';
    }, 1500);
  });

  dockSearchTrigger.addEventListener('click', (e) => {
    e.stopPropagation();
    setSearchPopoverOpen(!isSearchPopoverOpen);
  });

  dockSearchClose.addEventListener('click', (e) => {
    e.stopPropagation();
    setSearchPopoverOpen(false);
  });

  document.addEventListener('click', (e) => {
    if (isSearchPopoverOpen && !dockSearchAnchor.contains(e.target)) {
      setSearchPopoverOpen(false);
    }
  });

  dockButtons.forEach((btn) => {
    if (btn === dockSearchTrigger) return;
    btn.addEventListener('click', () => {
      const dockKey = btn.dataset.dock;
      setSearchPopoverOpen(false);
      if (dockKey === 'login') {
        history.replaceState(null, '', '/');
        switchCloudPanel('login');
      } else if (dockKey === 'register') {
        history.replaceState(null, '', '/');
        switchCloudPanel('register');
      } else if (dockKey === 'profile') {
        loadProfileById(currentViewedProfileId || getSessionUserId() || DEFAULT_PROFILE_ID);
      } else if (dockKey === 'me') {
        loadMyProfile();
      }
    });
  });

  dockSearchForm.addEventListener('submit', (e) => {
    e.preventDefault();
    const targetId = dockSearchInput.value.trim();
    setSearchPopoverOpen(false);
    if (targetId) {
      loadProfileById(targetId);
    }
  });

  // ===================== 12. BOOTSTRAP =====================
  initConstellation();
  startCloudMorphLoop();
  renderSessionHeader();
  updateGenderIndicator();

  window.addEventListener('resize', () => {
    updateGenderIndicator();
    const activeDock = appleDock.querySelector('.dock-btn.active');
    if (activeDock && activeDock.dataset.dock) {
      updateDockPill(activeDock.dataset.dock);
    }
  });

  const pathMatch = window.location.pathname.match(/^\/profile\/([0-9a-fA-F-]{36})$/);
  if (pathMatch) {
    loadProfileById(pathMatch[1]);
  } else {
    updateRightCopy('login');
    requestAnimationFrame(() => updateDockPill('login'));
  }
})();
