<template>
  <div :class="['app-wrapper', { 'dark-mode': isDark }]">
  <nav class="top-navbar">
    <div class="nav-brand">
      <img src="/Homesuite.png" alt="HomeSuite Logo" class="brand-logo" />
      <span class="brand-name">HomeSuite</span>
    </div>

      <div class="nav-links">
        <RouterLink to="/" class="nav-item" exact-active-class="active">
          <span class="nav-icon">📊</span> Dashboard
        </RouterLink>
        <RouterLink to="/budget" class="nav-item" active-class="active">
          <span class="nav-icon">💰</span> Budget
        </RouterLink>
        <RouterLink to="/shopping-lists" class="nav-item" active-class="active">
          <span class="nav-icon">🛒</span> Einkauf
        </RouterLink>
        <RouterLink to="/recipes" class="nav-item" active-class="active">
          <span class="nav-icon">🍳</span> Rezepte
        </RouterLink>
        <RouterLink to="/meal-planner" class="nav-item" active-class="active">
          <span class="nav-icon">📅</span> Meal Planner
        </RouterLink>
        <RouterLink to="/inventory" class="nav-item" active-class="active">
          <span class="nav-icon">📦</span> Inventar
        </RouterLink>
        <RouterLink to="/catalog" class="nav-item" active-class="active">
          <span class="nav-icon">🏷️</span> Katalog
        </RouterLink>
      </div>

      <button class="dark-toggle" @click="toggleDark" :title="isDark ? 'Light Mode' : 'Dark Mode'">
        <span class="toggle-track">
          <span class="toggle-thumb">{{ isDark ? '🌙' : '☀️' }}</span>
        </span>
      </button>
    </nav>

    <main class="page-content">
      <RouterView />
    </main>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'

const isDark = ref(false)

onMounted(() => {
  const saved = localStorage.getItem('homesuite-dark')
  if (saved === 'true') {
    isDark.value = true
  }
})

watch(isDark, (value) => {
  localStorage.setItem('homesuite-dark', String(value))
})

function toggleDark() {
  isDark.value = !isDark.value
}
</script>

<style>



:root {
  --bg: #f0f4f8;
  --surface: #ffffff;
  --surface2: #f8fafc;
  --border: #e2e8f0;
  --text: #1a202c;
  --text-muted: #64748b;
  --primary: #6366f1;
  --primary-light: #818cf8;
  --success: #10b981;
  --danger: #ef4444;
  --warning: #f59e0b;
  --nav-bg: #ffffff;
  --nav-shadow: 0 1px 20px rgba(0,0,0,0.08);
  --card-shadow: 0 4px 24px rgba(0,0,0,0.07);
  --radius: 16px;
  --radius-sm: 10px;
}

html,
body,
#app {
  min-height: 100%;
}

body {
  font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
  background: var(--bg);
  color: var(--text);
  transition: background 0.3s ease, color 0.3s ease;
}

.dark-mode {
  --bg: #0f172a;
  --surface: #1e293b;
  --surface2: #162032;
  --border: #2d3f57;
  --text: #f1f5f9;
  --text-muted: #94a3b8;
  --nav-bg: #1e293b;
  --nav-shadow: 0 1px 20px rgba(0,0,0,0.4);
  --card-shadow: 0 4px 24px rgba(0,0,0,0.3);
}

* {
  box-sizing: border-box;
}

body {
  margin: 0;
}

.app-wrapper {
  min-height: 100vh;
  background: var(--bg);
  color: var(--text);
}

.top-navbar {
  position: sticky;
  top: 0;
  z-index: 100;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 0 28px;
  height: 64px;
  background: var(--nav-bg);
  border-bottom: 1px solid var(--border);
  box-shadow: var(--nav-shadow);
  transition: background 0.3s ease, border-color 0.3s ease;
  border-radius: 16px;                 /* 👉 abgerundet */
  background: rgba(255, 255, 255, 0.7);
  backdrop-filter: blur(12px);         /* 👉 Glass-Effekt */
  -webkit-backdrop-filter: blur(12px);
}



/* Dark Mode */
.dark-mode .top-navbar {
  background: rgba(30, 30, 30, 0.7);
  border: 1px solid rgba(255, 255, 255, 0.08);
  box-shadow: 0 8px 25px rgba(0, 0, 0, 0.4);
}

/* Brand Bereich */
.nav-brand {
  display: flex;
  align-items: center;
  gap: 12px;
}

/* Logo */
.brand-logo {
  width: 36px;
  height: 36px;
  object-fit: contain;

  border-radius: 10px;                 /* 👉 Logo auch abgerundet */
  padding: 4px;

  background: rgba(255, 255, 255, 0.6);
}

/* Dark Mode Logo */
.dark-mode .brand-logo {
  background: rgba(255, 255, 255, 0.08);
}

.nav-brand {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-right: 24px;
  flex-shrink: 0;
}

.brand-icon {
  font-size: 24px;
}

.brand-name {
  font-size: 18px;
  font-weight: 700;
  color: var(--primary);
  letter-spacing: -0.5px;
}

.nav-links {
  display: flex;
  gap: 4px;
  flex: 1;
  flex-wrap: wrap;
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 7px;
  padding: 8px 16px;
  border-radius: 10px;
  color: var(--text-muted);
  text-decoration: none;
  font-size: 14px;
  font-weight: 500;
  transition: all 0.2s ease;
}

.nav-item:hover {
  background: rgba(99, 102, 241, 0.08);
  color: var(--primary);
}

.nav-item.active,
.nav-item.router-link-active {
  background: rgba(99, 102, 241, 0.12);
  color: var(--primary);
  font-weight: 600;
}

.nav-icon {
  font-size: 16px;
}

.dark-toggle {
  margin-left: auto;
  background: none;
  border: none;
  cursor: pointer;
  padding: 4px;
  flex-shrink: 0;
}

.toggle-track {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 42px;
  height: 42px;
  border-radius: 50%;
  background: var(--surface2);
  border: 2px solid var(--border);
  transition: all 0.2s ease;
}

.toggle-track:hover {
  border-color: var(--primary);
}

.toggle-thumb {
  font-size: 18px;
}

.page-content {
  min-height: calc(100vh - 64px);
  background: var(--bg);
  transition: background 0.3s ease;
}

@media (max-width: 1100px) {
  .top-navbar {
    height: auto;
    min-height: 64px;
    padding: 10px 16px;
    flex-wrap: wrap;
  }

  .nav-links {
    width: 100%;
  }

  .dark-toggle {
    margin-left: 0;
  }
}

@media (max-width: 768px) {
  .nav-links {
    gap: 2px;
  }

  .nav-item {
    padding: 7px 10px;
    font-size: 12px;
  }

  .brand-name {
    display: none;
  }
}
</style>

