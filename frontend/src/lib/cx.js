// Minimal classnames joiner — skips falsy values so components can do
// cx('base', condition && 'variant', className) without a dependency.
export function cx(...classes) {
  return classes.filter(Boolean).join(' ')
}
