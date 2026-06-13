<template>
  <BNavbar toggleable="lg" class="border-bottom mb-3">
    <BNavbarBrand to="/" class="d-flex align-items-center gap-2">
      <img src="/homesuite.png" alt="HomeSuite Logo" width="32" height="32" />
      <span class="fw-bold">HomeSuite</span>
    </BNavbarBrand>

    <BNavbarToggle target="nav-collapse" />

    <BCollapse id="nav-collapse" is-nav>
      <BNavbarNav>
        <BNavItem to="/" exact>📊 Dashboard</BNavItem>
        <BNavItem to="/budget">💰 Budget</BNavItem>
        <BNavItem to="/shopping-lists">🛒 Einkauf</BNavItem>
        <BNavItem to="/recipes">🍳 Rezepte</BNavItem>
        <BNavItem to="/inventory">📦 Inventar</BNavItem>
        <BNavItem to="/catalog">🏷️ Katalog</BNavItem>
      </BNavbarNav>

      <BNavbarNav class="ms-auto">
        <BNavItem @click="toggleDark" :title="isDark ? 'Light Mode' : 'Dark Mode'">
          {{ isDark ? '🌙' : '☀️' }}
        </BNavItem>
      </BNavbarNav>
    </BCollapse>
  </BNavbar>

  <main class="container-fluid pb-4">
    <RouterView />
  </main>
</template>

<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'

const isDark = ref(false)

function applyTheme(dark: boolean) {
  document.documentElement.setAttribute('data-bs-theme', dark ? 'dark' : 'light')
}

onMounted(() => {
  isDark.value = localStorage.getItem('homesuite-dark') === 'true'
  applyTheme(isDark.value)
})

watch(isDark, (value) => {
  localStorage.setItem('homesuite-dark', String(value))
  applyTheme(value)
})

function toggleDark() {
  isDark.value = !isDark.value
}
</script>
