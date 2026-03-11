import { computed, onMounted, onUnmounted, ref } from "vue";

export type ViewportMode = "mobile" | "tablet" | "desktop";

export const MOBILE_MAX_WIDTH = 768;
export const TABLET_MAX_WIDTH = 1023;

function resolveMode(width: number): ViewportMode {
  if (width <= MOBILE_MAX_WIDTH) {
    return "mobile";
  }

  if (width <= TABLET_MAX_WIDTH) {
    return "tablet";
  }

  return "desktop";
}

export function createViewportState(width: number) {
  const mode = resolveMode(width);

  return {
    width,
    mode,
    isMobile: mode === "mobile",
    isTablet: mode === "tablet",
    isDesktop: mode === "desktop",
    isTabletOrBelow: mode !== "desktop",
  };
}

export function useViewport() {
  const width = ref(
    typeof window === "undefined" ? TABLET_MAX_WIDTH + 1 : window.innerWidth,
  );

  const updateWidth = () => {
    width.value = window.innerWidth;
  };

  onMounted(() => {
    updateWidth();
    window.addEventListener("resize", updateWidth, { passive: true });
  });

  onUnmounted(() => {
    window.removeEventListener("resize", updateWidth);
  });

  const viewport = computed(() => createViewportState(width.value));

  return {
    width,
    viewport,
    mode: computed(() => viewport.value.mode),
    isMobile: computed(() => viewport.value.isMobile),
    isTablet: computed(() => viewport.value.isTablet),
    isDesktop: computed(() => viewport.value.isDesktop),
    isTabletOrBelow: computed(() => viewport.value.isTabletOrBelow),
  };
}
