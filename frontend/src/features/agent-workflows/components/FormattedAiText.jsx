// Agent 3/4's LLM-generated narrative text (selectionJustification, validation.explanation,
// shipperMessage) sometimes comes back with **bold** markers and inline "1. ... 2. ..." lists,
// since nothing in their prompts forbids markdown-style formatting. This renders just those two
// constructs properly instead of showing literal asterisks and a numbered wall of text - not a
// general markdown parser, since nothing else the AI produces here needs one.

function renderInlineBold(text, keyPrefix) {
  return text.split(/(\*\*[^*]+?\*\*)/g).map((part, i) =>
    part.startsWith('**') && part.endsWith('**') ? (
      <strong key={`${keyPrefix}-${i}`}>{part.slice(2, -2)}</strong>
    ) : (
      <span key={`${keyPrefix}-${i}`}>{part}</span>
    ),
  )
}

// Detects an inline numbered list like "... 1. **Label:** text 2. **Label:** text" squashed into
// one sentence, and splits it into { leading, items }. Returns null if fewer than two numbered
// markers are found, so a normal sentence with an incidental "24. Main Street" isn't mangled.
function splitIntoListItems(paragraph) {
  const markerPattern = /(?:^|\s)(\d{1,2})\.\s+/g
  const matches = [...paragraph.matchAll(markerPattern)]
  if (matches.length < 2) return null

  const items = []
  for (let i = 0; i < matches.length; i += 1) {
    const start = matches[i].index + matches[i][0].length
    const end = i + 1 < matches.length ? matches[i + 1].index : paragraph.length
    const itemText = paragraph.slice(start, end).trim()
    if (itemText) items.push(itemText)
  }
  if (items.length < 2) return null

  const leading = paragraph.slice(0, matches[0].index).trim()
  return { leading, items }
}

export default function FormattedAiText({ text, className = '' }) {
  if (!text) return null

  const paragraphs = text
    .split(/\n+/)
    .map((p) => p.trim())
    .filter(Boolean)

  return (
    <div className={className}>
      {paragraphs.map((paragraph, pIdx) => {
        const list = splitIntoListItems(paragraph)
        if (list) {
          return (
            <div key={pIdx} className={pIdx > 0 ? 'mt-2' : ''}>
              {list.leading && <p>{renderInlineBold(list.leading, `lead-${pIdx}`)}</p>}
              <ol className="mt-1.5 list-decimal space-y-1 pl-5">
                {list.items.map((item, iIdx) => (
                  <li key={iIdx}>{renderInlineBold(item, `item-${pIdx}-${iIdx}`)}</li>
                ))}
              </ol>
            </div>
          )
        }
        return (
          <p key={pIdx} className={pIdx > 0 ? 'mt-2' : ''}>
            {renderInlineBold(paragraph, `p-${pIdx}`)}
          </p>
        )
      })}
    </div>
  )
}
