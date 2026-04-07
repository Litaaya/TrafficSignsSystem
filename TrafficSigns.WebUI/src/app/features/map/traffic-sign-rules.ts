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
  { id: 'car', label: 'Car', icon: '🚗' },
  { id: 'passenger_car', label: 'Passenger', icon: '🚌' },
  { id: 'truck', label: 'Truck', icon: '🚚' },
  { id: 'container', label: 'Container', icon: '🚛' },
  { id: 'motorcycle', label: 'Motorcycle', icon: '🏍️' }
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
