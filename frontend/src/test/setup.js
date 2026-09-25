import '@testing-library/jest-dom/vitest'

// jsdom doesn't implement matchMedia, and several components (map/tailwind
// utilities) probe it indirectly via libraries that assume a real browser.
if (!window.matchMedia) {
  window.matchMedia = (query) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  })
}
