import { cx } from '../lib/cx.js'

// The "card surface" base style per DESIGN.md > Elevation & Depth /
// Components > Cards: pure white surface, 6px radius, 1px Slate-200
// hairline border, the one soft shadow token, minimal 16px padding.
//
// Card.Image/Header/Body/Footer are opt-in composition slots (per
// .claude/rules/frontend-design.md #2 — composition over configuration):
// plain JSX inside <Card> keeps working exactly as before, unchanged. Use
// the slots when a screen needs an edge-to-edge image, a bordered
// header/footer row, or independently-padded sections instead of growing
// this component's prop list. Each slot brings its own padding, so a
// compound card drops the base padding to let them control spacing:
//
//   <Card className="overflow-hidden p-0">
//     <Card.Image src={cover} alt="" className="h-40" />
//     <Card.Header>Title</Card.Header>
//     <Card.Body>Body content…</Card.Body>
//     <Card.Footer className="flex items-center justify-between">
//       <span>Footer content</span>
//     </Card.Footer>
//   </Card>
//
// No shared state between the slots (unlike a tabs/accordion compound
// component), so this is plain namespaced sub-components rather than a
// createContext()-backed pattern — there's nothing for them to coordinate.
function Card({ className, children, ...props }) {
  return (
    <div
      className={cx('rounded-md border border-slate-border bg-surface-container-lowest p-4 shadow-soft', className)}
      {...props}
    >
      {children}
    </div>
  )
}

// Bleeds to the card's full width — pair the outer Card with `overflow-hidden`
// so the image's corners still respect the card's rounded-md.
function CardImage({ className, alt = '', ...props }) {
  return <img alt={alt} className={cx('block w-full object-cover', className)} {...props} />
}

function CardHeader({ className, children, ...props }) {
  return (
    <div className={cx('border-b border-slate-border px-4 py-3', className)} {...props}>
      {children}
    </div>
  )
}

function CardBody({ className, children, ...props }) {
  return (
    <div className={cx('p-4', className)} {...props}>
      {children}
    </div>
  )
}

function CardFooter({ className, children, ...props }) {
  return (
    <div className={cx('border-t border-slate-border px-4 py-3', className)} {...props}>
      {children}
    </div>
  )
}

Card.Image = CardImage
Card.Header = CardHeader
Card.Body = CardBody
Card.Footer = CardFooter

export default Card
