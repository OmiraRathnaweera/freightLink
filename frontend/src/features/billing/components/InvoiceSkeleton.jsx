import Skeleton from '../../../components/Skeleton.jsx'

function InvoiceSkeleton() {
  return (
    <div className="p-4 space-y-3">
      {Array.from({ length: 6 }).map((_, i) => (
        <div key={i} className="flex items-center gap-4 py-2">
          <Skeleton className="h-6 w-24 rounded" />
          <Skeleton className="h-6 w-48 rounded" />
          <Skeleton className="h-6 w-20 rounded" />
          <Skeleton className="h-6 w-28 rounded" />
          <Skeleton className="h-6 w-24 rounded" />
          <Skeleton className="h-6 w-20 rounded" />
          <Skeleton className="h-6 w-24 rounded" />
        </div>
      ))}
    </div>
  )
}

export default InvoiceSkeleton
