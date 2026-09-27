import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../data/trips_repository.dart';
import '../models/trip_models.dart';

/// Agency Staff view of the trips already dispatched by their agency.
class AgencyTripsScreen extends StatefulWidget {
  const AgencyTripsScreen({super.key});

  @override
  State<AgencyTripsScreen> createState() => _AgencyTripsScreenState();
}

class _AgencyTripsScreenState extends State<AgencyTripsScreen> {
  late Future<List<TripResponse>> _trips;

  @override
  void initState() {
    super.initState();
    _trips = _loadTrips();
  }

  Future<List<TripResponse>> _loadTrips() async {
    final page = await context.read<TripsRepository>().getTrips(pageSize: 100, sortBy: 'createdAt', sortDir: 'desc');
    return page.items;
  }

  void _refresh() => setState(() => _trips = _loadTrips());

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Agency Trips'),
        actions: [IconButton(onPressed: _refresh, icon: const Icon(Icons.refresh))],
      ),
      body: FutureBuilder<List<TripResponse>>(
        future: _trips,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator());
          if (snapshot.hasError) return Center(child: Text('Could not load trips: ${snapshot.error}'));
          final trips = snapshot.data ?? const [];
          if (trips.isEmpty) return const Center(child: Text('No dispatched trips yet.'));
          return RefreshIndicator(
            onRefresh: () async => _refresh(),
            child: ListView.separated(
              padding: const EdgeInsets.all(16),
              itemCount: trips.length,
              separatorBuilder: (_, _) => const SizedBox(height: 8),
              itemBuilder: (context, index) {
                final trip = trips[index];
                return Card(
                  child: ListTile(
                    leading: const Icon(Icons.route_outlined),
                    title: Text(trip.referenceCode ?? 'Trip ${trip.tripId.substring(0, 8)}'),
                    subtitle: Text('${trip.pickupAddress} → ${trip.dropoffAddress}'),
                    trailing: Chip(label: Text(trip.status)),
                  ),
                );
              },
            ),
          );
        },
      ),
    );
  }
}
