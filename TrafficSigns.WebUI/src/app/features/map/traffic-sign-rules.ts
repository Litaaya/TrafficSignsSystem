export interface SignTemplate {
  code: string;
  name: string;
  subcategory: string;
  hasLanes: boolean;
  hasVehicles: boolean | 'optional';
  hasTime: boolean;
}

export const SPEED_OPTIONS = [30, 40, 50, 60, 70, 80, 90, 100, 120];

export const VEHICLE_OPTIONS = [
  { id: 'passenger_car', label: 'Passenger Car', icon: '🚗', description: 'Cars with 4-7 seats' },
  { id: 'bus_coach', label: 'Bus/Coach', icon: '🚌', description: 'Inter-province buses, contract coaches' },
  { id: 'city_bus', label: 'City Bus', icon: '🚐', description: 'Urban public transit buses' },

  { id: 'truck_light', label: 'Light Truck', icon: '🛻', description: 'Payload capacity < 3.5 tons' },
  { id: 'truck_heavy', label: 'Heavy Truck', icon: '🚚', description: 'Payload capacity ≥ 3.5 tons' },
  { id: 'tractor_trailer', label: 'Tractor-Trailer', icon: '🚛', description: 'Container trucks, semi-trailers' },
  { id: 'tanker', label: 'Tanker', icon: '⛽', description: 'Liquid cargo, fuel or chemical tankers' },
  { id: 'special_purpose', label: 'Special Purpose Vehicle', icon: '🚜', description: 'Construction, agricultural, or forestry vehicles' },

  { id: 'motorcycle', label: 'Motorcycle', icon: '🏍️', description: 'Engine capacity ≥ 50cm³' },
  { id: 'moped', label: 'Moped', icon: '🛵', description: 'Engine capacity < 50cm³, includes electric bikes' },
  { id: 'motor_tricycle', label: 'Motor Tricycle', icon: '🛺', description: '3-wheeled motorized vehicles' },
  { id: 'quadricycle', label: 'Motorized Quadricycle', icon: '🔋', description: 'Electric tourist cars, small freight 4-wheelers' }
];

export const SIGN_TEMPLATES: SignTemplate[] = [
  { code: 'P.127', name: 'Maximum Speed Limit', subcategory: 'MAX_SPEED', hasLanes: false, hasVehicles: false, hasTime: false },
  { code: 'P.127a', name: 'Night Maximum Speed Limit', subcategory: 'MAX_SPEED', hasLanes: false, hasVehicles: false, hasTime: true },
  { code: 'P.127b', name: 'Speed Limit Per Lane', subcategory: 'MAX_SPEED', hasLanes: true, hasVehicles: false, hasTime: false },
  { code: 'P.127c', name: 'Speed Limit Per Lane & Vehicle', subcategory: 'MAX_SPEED', hasLanes: true, hasVehicles: true, hasTime: false },
  { code: 'DP.134', name: 'End Maximum Speed Limit', subcategory: 'END_MAX_SPEED', hasLanes: false, hasVehicles: false, hasTime: false },
  { code: 'DP.127', name: 'End Speed Limit Multi-Lane', subcategory: 'END_MAX_SPEED', hasLanes: true, hasVehicles: 'optional', hasTime: false },
  { code: 'R.306', name: 'Minimum Speed Limit', subcategory: 'MIN_SPEED', hasLanes: false, hasVehicles: false, hasTime: false },
  { code: 'R.307', name: 'End Minimum Speed Limit', subcategory: 'END_MIN_SPEED', hasLanes: false, hasVehicles: false, hasTime: false },
  { code: 'R.E,9d', name: 'Electronic Maximum Speed', subcategory: 'MAX_SPEED', hasLanes: false, hasVehicles: false, hasTime: false },
  { code: 'R.E,10d', name: 'Electronic Maximum Speed', subcategory: 'MAX_SPEED', hasLanes: false, hasVehicles: false, hasTime: false }
];
