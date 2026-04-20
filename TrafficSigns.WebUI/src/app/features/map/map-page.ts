import { Component, OnInit, AfterViewInit, NgZone, ChangeDetectorRef, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import * as L from 'leaflet';
import { OsmRoadService } from './osm-road.service';
import { SIGN_TEMPLATES, SPEED_OPTIONS, VEHICLE_OPTIONS, SignTemplate } from './traffic-sign-rules';
import { AuthService } from '../../core/services/auth-service';

@Component({
  selector: 'app-map-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './map-page.html',
  styles: [`
    .custom-scrollbar::-webkit-scrollbar { width: 4px; height: 4px; }
    .custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
    .custom-scrollbar::-webkit-scrollbar-thumb { background: #cbd5e1; border-radius: 10px; transition: all 0.3s; }
    .custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #94a3b8; }
    .custom-scrollbar { scrollbar-width: thin; scrollbar-color: #cbd5e1 transparent; }
    @keyframes flow { from { stroke-dashoffset: 20; } to { stroke-dashoffset: 0; } }
    ::ng-deep .flow-animation { animation: flow 1s linear infinite !important; stroke-dasharray: 10, 10 !important; }
    ::ng-deep .static-dash { stroke-dashoffset: 0 !important; stroke-dasharray: 10, 10 !important; }
  `]
})
export class MapPageComponent implements OnInit, AfterViewInit {
  private map!: L.Map;
  private roadLayer: L.GeoJSON | null = null;
  private signLayer: L.LayerGroup = new L.LayerGroup();
  private selectionMarker: L.CircleMarker | null = null;
  private directionPolyline: L.Polyline | null = null;
  private roadGlowLayer: L.Polyline | null = null;

  isAccountDropdownOpen = false;
  accountSearchTerm = '';
  hasNoAccounts: boolean = false;
  accounts: any[] = [];
  selectedAccountId: string = '';
  sidebarState: 'HIDDEN' | 'ROAD_INFO' | 'WIZARD' = 'HIDDEN';
  wizardStep: 'MAIN_CATEGORY' | 'SUB_CATEGORY' | 'TYPE' | 'FORM' | 'REVIEW' = 'MAIN_CATEGORY';

  showDeletedMode: boolean = false;
  showConfirmModal: boolean = false;
  confirmActionType: 'DELETE' | 'REACTIVATE' | null = null;

  isSaving: boolean = false;
  selectedMainCategory: string = '';
  selectedSubCategory: string = '';
  selectedRoadInfo: any = null;
  selectedTemplate: SignTemplate | null = null;
  laneCount: number = 1;
  lanes: any[] = [];
  activeLaneIndex: number = 0;
  clickedLatLng: { lat: number; lng: number } | null = null;
  selectedSegmentId: number | null = null;
  selectedSignId: string | null = null;
  selectedSignData: any = null;
  segmentCoords: any[] = [];

  isForward: boolean = true;
  isDirectionSelected: boolean = false;
  forwardCompass: string = '';
  backwardCompass: string = '';

  startTime: string = '22:00';
  endTime: string = '05:00';
  dp127HasVehicles: boolean = false;

  associatedRoadData: any = null;
  isViewingRoadFromSign: boolean = false;

  mouseLatLng: string = '0.000000, 0.000000';

  readonly ROAD_LABELS: { [key: string]: string } = {
    name: 'Road',
    segmentId: 'Road Id',
    highwayId: 'Road Type',
    onewayType: 'OnewayType'
  };
  readonly speedOptions = SPEED_OPTIONS;
  readonly vehicleOptions = VEHICLE_OPTIONS;
  readonly signTemplates = SIGN_TEMPLATES;

  constructor(private osmService: OsmRoadService, private zone: NgZone, private cdr: ChangeDetectorRef, private authService: AuthService) { }

  get currentAccountRole(): string {
    const currentAcc = this.accounts.find(a => a.accountId === this.selectedAccountId);
    return currentAcc?.role || 'Viewer';
  }

  get isOwner(): boolean {
    return this.currentAccountRole === 'Owner';
  }

  get canEdit(): boolean {
    return this.currentAccountRole === 'Owner' || this.currentAccountRole === 'Member';
  }

  openAccountUserManager() {
    console.log("Open User management for Account:", this.selectedAccountId);
  }

  ngOnInit(): void {
    this.loadAccounts();
  }

  ngAfterViewInit(): void {
  }

  loadAccounts() {
    const userId = this.authService.getUserId();
    if (!userId) return;

    this.osmService.getAccountsOfUser(userId).subscribe({
      next: (res) => {
        this.accounts = res;

        if (!this.accounts || this.accounts.length === 0) {
          this.hasNoAccounts = true;
          this.cdr.detectChanges();
          return;
        }

        this.hasNoAccounts = false;
        const savedAccountId = localStorage.getItem('lastSelectedAccountId');

        const found = this.accounts.find(a => a.accountId === savedAccountId);

        if (savedAccountId && found) {
          this.selectedAccountId = savedAccountId;
        } else {
          this.selectedAccountId = this.accounts[0].accountId;
        }

        if (!this.map) {
          setTimeout(() => this.initMap(), 0);
        } else {
          this.loadTrafficSigns();
        }

        this.cdr.detectChanges();
      },
      error: () => {
        this.hasNoAccounts = true;
        this.cdr.detectChanges();
      }
    });
  }

  get filteredAccounts() {
    if (!this.accountSearchTerm) return this.accounts;
    return this.accounts.filter(acc =>
      acc.accountName.toLowerCase().includes(this.accountSearchTerm.toLowerCase())
    );
  }

  selectAccount(acc: any) {
    this.selectedAccountId = acc.accountId;
    this.accountSearchTerm = '';
    this.isAccountDropdownOpen = false;
    this.onAccountChange();
  }

  @HostListener('document:click', ['$event'])
  onClickOutside(event: Event) {
    const target = event.target as HTMLElement;
    if (!target.closest('.account-dropdown-container')) {
      this.isAccountDropdownOpen = false;
    }
  }

  onAccountChange() {
    localStorage.setItem('lastSelectedAccountId', this.selectedAccountId);
    this.closeSidebar();
    this.loadTrafficSigns();
  }

  private initMap(): void {
    const savedLat = localStorage.getItem('map_lat') || '10.762622';
    const savedLng = localStorage.getItem('map_lng') || '106.660172';
    const savedZoom = localStorage.getItem('map_zoom') || '15';
    const vietnamBounds = L.latLngBounds(L.latLng(8.0, 102.0), L.latLng(23.5, 110.0));

    const mapElement = document.getElementById('map');
    if (!mapElement) return;

    this.map = L.map('map', {
      doubleClickZoom: false, zoomControl: true,
      maxBounds: vietnamBounds, maxBoundsViscosity: 1.0, minZoom: 5
    }).setView([parseFloat(savedLat), parseFloat(savedLng)], parseInt(savedZoom));

    this.map.on('mousemove', (e: L.LeafletMouseEvent) => {
      this.zone.run(() => {
        this.mouseLatLng = `${e.latlng.lat.toFixed(6)}, ${e.latlng.lng.toFixed(6)}`;
      });
    });

    this.map.createPane('glowPane');
    this.map.getPane('glowPane')!.style.zIndex = '350';
    this.map.createPane('directionPane');
    this.map.getPane('directionPane')!.style.zIndex = '450';
    this.map.createPane('markerPane');
    this.map.getPane('markerPane')!.style.zIndex = '610';

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19 }).addTo(this.map);
    this.signLayer.addTo(this.map);

    this.loadTrafficSigns();

    this.map.on('moveend', () => {
      const center = this.map.getCenter();
      localStorage.setItem('map_lat', center.lat.toString());
      localStorage.setItem('map_lng', center.lng.toString());
      localStorage.setItem('map_zoom', this.map.getZoom().toString());
      this.loadRoadsInView();
    });

    this.map.on('click', () => {
      this.zone.run(() => {
        this.closeSidebar();
      });
    });

    this.loadRoadsInView();
  }

  private loadRoadsInView(): void {
    if (!this.map) return;
    const bounds = this.map.getBounds();
    this.osmService.getRoadsInView(bounds.getSouth(), bounds.getWest(), bounds.getNorth(), bounds.getEast(), this.map.getZoom())
      .subscribe(roads => {
        if (this.roadLayer) this.map.removeLayer(this.roadLayer);
        const geoJsonFeatures: any = roads.map(r => ({ type: 'Feature', properties: { ...r }, geometry: r.geometry }));
        this.roadLayer = L.geoJSON(geoJsonFeatures, {
          style: (f: any) => ({
            color: f.properties.segmentId === this.selectedSegmentId ? '#ffffff' : 'transparent',
            weight: f.properties.segmentId === this.selectedSegmentId ? 10 : 20,
            opacity: f.properties.segmentId === this.selectedSegmentId ? 0.8 : 0
          }),
          onEachFeature: (f, layer) => {
            layer.on('click', (e) => L.DomEvent.stopPropagation(e));
            layer.on('dblclick', (e: any) => {
              const coords = (f.geometry as any).coordinates.map((c: any[]) => ({ lat: c[1], lng: c[0] }));
              this.onRoadClicked(f.properties, coords, e.latlng);
              L.DomEvent.stopPropagation(e);
            });
          }
        }).addTo(this.map);
      });
  }

  loadTrafficSigns() {
    if (!this.selectedAccountId || this.selectedAccountId === 'undefined' || !this.map) {
      console.warn('Error: No Account Id');
      return;
    }
    this.osmService.getTrafficSigns(this.selectedAccountId).subscribe(signs => {
      this.signLayer.clearLayers();
      const filteredSigns = signs.filter(s => (s.isDeleted || s.IsDeleted || false) === this.showDeletedMode);

      filteredSigns.forEach(s => {
        const m = L.circleMarker([s.location.coordinates[1], s.location.coordinates[0]], {
          radius: 8,
          fillColor: this.showDeletedMode ? '#94a3b8' : '#ef4444',
          color: '#fff',
          weight: 2,
          fillOpacity: 1,
          pane: 'markerPane'
        });

        m.on('click', (e) => {
          L.DomEvent.stopPropagation(e);
          this.zone.run(() => {
            this.showExistingSignInfo(s);
          });
        });

        this.signLayer.addLayer(m);
      });
    });
  }

  toggleViewMode() {
    this.showDeletedMode = !this.showDeletedMode;
    this.closeSidebar();
    this.loadTrafficSigns();
  }

  get isTwoWayRoad(): boolean {
    if (!this.selectedRoadInfo || this.selectedSignId) return true;
    const ow = this.selectedRoadInfo.oneway;
    return ow === 0 || ow === '0' || ow === 'no' || !ow;
  }

  onRoadClicked(roadInfo: any, coords: any[], clickLatLng: any) {
    this.zone.run(() => {
      this.selectedSignId = null;
      this.selectedSignData = null;
      this.selectedSegmentId = roadInfo.segmentId;
      this.selectedRoadInfo = roadInfo;
      this.segmentCoords = coords;
      this.clickedLatLng = { lat: clickLatLng.lat, lng: clickLatLng.lng };
      this.sidebarState = 'ROAD_INFO';

      const ow = roadInfo.oneway;
      if (ow === 1 || ow === '1' || ow === 'yes') {
        this.isForward = true;
        this.isDirectionSelected = true;
      } else if (ow === -1 || ow === '-1') {
        this.isForward = false;
        this.isDirectionSelected = true;
      } else {
        this.isDirectionSelected = false;
      }

      this.updateVisuals();
      this.cdr.detectChanges();
    });
  }

  showExistingSignInfo(sign: any) {
    this.isViewingRoadFromSign = false;
    this.selectedSignData = sign;
    this.selectedSignId = sign.id;
    this.selectedSegmentId = sign.roadSegmentId;
    this.clickedLatLng = { lat: sign.location.coordinates[1], lng: sign.location.coordinates[0] };
    this.isForward = sign.isForwardDirection;
    this.isDirectionSelected = true;

    const displayInfo: any = {
      'Sign Code': sign.code,
      'Name': sign.name,
      'Direction': sign.isForwardDirection ? 'Forward' : 'Backward'
    };

    if (sign.metadata) {
      if (sign.metadata.startTime && sign.metadata.endTime) {
        displayInfo['Active Time'] = `${sign.metadata.startTime} - ${sign.metadata.endTime}`;
      }

      if (sign.metadata.totalLanes) {
        displayInfo['Total Lanes'] = sign.metadata.totalLanes;
        if (sign.metadata.lanes && Array.isArray(sign.metadata.lanes)) {
          sign.metadata.lanes.forEach((lane: any) => {
            let laneDetail = `${lane.speed ? lane.speed + ' km/h' : 'N/A'}`;
            if (lane.vehicleTypes && lane.vehicleTypes.length > 0) {
              const vehicles = lane.vehicleTypes.map((v: string) => v.toUpperCase()).join(', ');
              laneDetail += ` [${vehicles}]`;
            }
            displayInfo[`Lane ${lane.laneNumber}`] = laneDetail;
          });
        }
      } else if (sign.metadata.value) {
        displayInfo['Speed Limit'] = `${sign.metadata.value} km/h`;
      }
    }

    this.selectedRoadInfo = displayInfo;
    this.sidebarState = 'ROAD_INFO';

    const lat = sign.location.coordinates[1];
    const lng = sign.location.coordinates[0];
    const offset = 0.01;

    this.osmService.getRoadsInView(lat - offset, lng - offset, lat + offset, lng + offset, 18)
      .subscribe(roads => {
        const road = roads.find(r => r.segmentId === sign.roadSegmentId);
        if (road) {
          this.associatedRoadData = road;
          this.segmentCoords = (road.geometry as any).coordinates.map((c: any[]) => ({ lat: c[1], lng: c[0] }));
          this.updateVisuals();
          this.cdr.detectChanges();
        }
      });
    this.cdr.detectChanges();
  }

  viewRoadFromSign() {
    if (!this.associatedRoadData) return;
    this.isViewingRoadFromSign = true;
    const { geometry, ...displayInfo } = this.associatedRoadData;
    this.selectedRoadInfo = displayInfo;
    this.sidebarState = 'ROAD_INFO';
    this.cdr.detectChanges();
  }

  goBackToSign() {
    if (this.selectedSignData) {
      this.isViewingRoadFromSign = false;
      this.showExistingSignInfo(this.selectedSignData);
    }
  }

  editSign() {
    if (!this.selectedSignData) return;
    const s = this.selectedSignData;

    const targetCode = (s.code || '').trim().toUpperCase();
    this.selectedTemplate = this.signTemplates.find(t => t.code.toUpperCase() === targetCode) || null;

    if (!this.selectedTemplate) {
      alert(`Template not found for code: ${s.code}`);
      return;
    }

    this.sidebarState = 'WIZARD';
    this.wizardStep = 'FORM';
    this.isForward = s.isForwardDirection;
    this.isDirectionSelected = true;
    this.lanes = [];

    if (s.metadata) {
      if (s.metadata.startTime) this.startTime = s.metadata.startTime;
      if (s.metadata.endTime) this.endTime = s.metadata.endTime;

      if (this.selectedTemplate.hasLanes && s.metadata.lanes) {
        this.laneCount = s.metadata.totalLanes || s.metadata.lanes.length;
        this.lanes = s.metadata.lanes.map((l: any) => ({
          n: l.laneNumber,
          v: l.speed,
          t: l.vehicleTypes ? [...l.vehicleTypes] : []
        }));
      } else {
        this.laneCount = 1;
        this.lanes = [{ n: 1, v: s.metadata.value || null, t: [] }];
      }

      this.dp127HasVehicles = this.selectedTemplate.hasVehicles === 'optional' && s.metadata.lanes?.some((l: any) => l.vehicleTypes?.length > 0);
    } else {
      this.initForm();
    }

    this.activeLaneIndex = 0;
    this.cdr.detectChanges();
  }

  private updateVisuals() {
    if (!this.map) return;
    if (this.selectionMarker) this.map.removeLayer(this.selectionMarker);
    if (this.roadGlowLayer) this.map.removeLayer(this.roadGlowLayer);
    if (this.directionPolyline) this.map.removeLayer(this.directionPolyline);

    if (this.clickedLatLng) {
      this.selectionMarker = L.circleMarker([this.clickedLatLng.lat, this.clickedLatLng.lng], {
        radius: 9, fillColor: this.showDeletedMode ? '#94a3b8' : '#ef4444', color: '#fff', weight: 3, fillOpacity: 1, pane: 'markerPane'
      }).addTo(this.map);
    }

    if (this.segmentCoords.length > 0) {
      this.roadGlowLayer = L.polyline(this.segmentCoords, {
        color: '#ffffff', weight: 14, opacity: 0.4, lineCap: 'round', pane: 'glowPane'
      }).addTo(this.map);

      let flowCoords = [...this.segmentCoords];
      if (this.isDirectionSelected && !this.isForward) {
        flowCoords.reverse();
      }

      const animationClass = this.isDirectionSelected ? 'flow-animation' : 'static-dash';

      this.directionPolyline = L.polyline(flowCoords, {
        color: '#fbbf24', weight: 5, opacity: 1, lineCap: 'round',
        dashArray: '10, 10', className: animationClass, pane: 'directionPane'
      }).addTo(this.map);
    }

    if (this.roadLayer) {
      this.roadLayer.setStyle((f: any) => ({
        color: f.properties.segmentId === this.selectedSegmentId ? '#ffffff' : 'transparent',
        weight: f.properties.segmentId === this.selectedSegmentId ? 10 : 20,
        opacity: f.properties.segmentId === this.selectedSegmentId ? 0.8 : 0
      }));
    }
  }

  setDirectionManual(forward: boolean) {
    if (this.isDirectionSelected && this.isForward === forward) {
      this.isDirectionSelected = false;
    } else {
      this.isForward = forward;
      this.isDirectionSelected = true;
    }
    this.updateVisuals();
    this.cdr.detectChanges();
  }

  toggleSpeed(lane: any, speed: number) {
    lane.v = (lane.v === speed) ? null : speed;
    this.cdr.detectChanges();
  }

  toggleVehicle(lane: any, vehicleId: string) {
    if (!lane.t) lane.t = [];
    const index = lane.t.indexOf(vehicleId);
    if (index > -1) {
      lane.t.splice(index, 1);
    } else {
      lane.t.push(vehicleId);
    }
    this.cdr.detectChanges();
  }

  clearAllVisuals() {
    if (!this.map) return;
    if (this.selectionMarker) this.map.removeLayer(this.selectionMarker);
    if (this.roadGlowLayer) this.map.removeLayer(this.roadGlowLayer);
    if (this.directionPolyline) this.map.removeLayer(this.directionPolyline);
    this.selectedSegmentId = null;
    if (this.roadLayer) {
      this.roadLayer.setStyle(() => ({ color: 'transparent', opacity: 0 }));
    }
  }

  closeSidebar() {
    this.zone.run(() => {
      this.sidebarState = 'HIDDEN';
      this.selectedSignId = null;
      this.selectedSignData = null;
      this.selectedRoadInfo = null;
      this.clearAllVisuals();
      this.resetWizard();
      this.cdr.detectChanges();
    });
  }

  startWizard() {
    this.sidebarState = 'WIZARD';
    this.wizardStep = 'MAIN_CATEGORY';
  }

  exitWizard() {
    this.sidebarState = 'ROAD_INFO';
    this.cdr.detectChanges();
  }

  selectMainCategory(c: string) {
    this.selectedMainCategory = c;
    this.wizardStep = 'SUB_CATEGORY';
  }

  selectSubCategory(c: string) {
    this.selectedSubCategory = c;
    this.wizardStep = 'TYPE';
  }

  get filteredTemplates() {
    return this.signTemplates.filter(t => t.subcategory === this.selectedSubCategory);
  }

  selectTemplate(t: SignTemplate) {
    this.selectedTemplate = t;
    this.initForm();
    this.wizardStep = 'FORM';
  }

  initForm() {
    this.laneCount = 1;
    this.activeLaneIndex = 0;
    this.startTime = '22:00';
    this.endTime = '05:00';
    this.dp127HasVehicles = false;
    this.lanes = [];
    this.updateLanesArray(1);
  }

  updateLanesArray(count: number) {
    this.laneCount = count;
    if (count > this.lanes.length) {
      const startCount = this.lanes.length;
      for (let i = 0; i < count - startCount; i++) {
        this.lanes.push({ n: this.lanes.length + 1, v: null, t: [] });
      }
    } else if (count < this.lanes.length) {
      this.lanes.splice(count);
    }
    if (this.activeLaneIndex >= count) {
      this.activeLaneIndex = count - 1;
    }
    this.cdr.detectChanges();
  }

  isVehicleRequired(): boolean {
    if (this.selectedTemplate?.hasVehicles === true) return true;
    if (this.selectedTemplate?.hasVehicles === 'optional' && this.dp127HasVehicles) return true;
    return false;
  }

  get isFormValid(): boolean {
    if (!this.selectedTemplate || !this.isDirectionSelected) return false;

    if (!this.selectedTemplate.hasLanes) {
      return this.lanes.length > 0 && this.lanes[0].v !== null;
    }

    return this.lanes.every(lane => {
      const hasSpeed = lane.v !== null;
      const hasVehicle = this.isVehicleRequired() ? (lane.t && lane.t.length > 0) : true;
      return hasSpeed && hasVehicle;
    });
  }

  goToReview() {
    if (this.isFormValid) this.wizardStep = 'REVIEW';
  }

  saveSign() {
    if (this.isSaving || !this.selectedTemplate || !this.clickedLatLng || !this.selectedSegmentId || !this.selectedAccountId) return;

    this.isSaving = true;
    this.cdr.detectChanges();

    const metadata: any = {};
    if (this.selectedTemplate.hasTime) {
      metadata.startTime = this.startTime;
      metadata.endTime = this.endTime;
    }

    if (this.selectedTemplate.hasLanes) {
      metadata.totalLanes = this.laneCount;
      metadata.lanes = this.lanes.map(l => {
        const laneData: any = { laneNumber: l.n, speed: l.v };
        if (this.isVehicleRequired()) {
          laneData.vehicleTypes = l.t;
        }
        return laneData;
      });
    } else {
      metadata.value = this.lanes[0]?.v;
    }

    const payload = {
      id: this.selectedSignId,
      code: this.selectedTemplate.code,
      name: this.selectedTemplate.name,
      latitude: this.clickedLatLng.lat,
      longitude: this.clickedLatLng.lng,
      accountId: this.selectedAccountId,
      roadSegmentId: this.selectedSegmentId,
      isForwardDirection: this.isForward,
      metadata: metadata
    };

    const handleResponse = {
      next: () => {
        this.zone.run(() => {
          this.isSaving = false;
          this.loadTrafficSigns();
          this.closeSidebar();
          this.cdr.detectChanges();
        });
      },
      error: (e: any) => {
        this.isSaving = false;
        alert(e.error?.message || 'Error saving sign');
        this.cdr.detectChanges();
      }
    };

    if (this.selectedSignId) {
      this.osmService.updateTrafficSign(this.selectedSignId, payload).subscribe(handleResponse);
    } else {
      this.osmService.createTrafficSign(payload).subscribe(handleResponse);
    }
  }

  deleteSign() {
    this.confirmActionType = 'DELETE';
    this.showConfirmModal = true;
  }

  reactivateSign() {
    this.confirmActionType = 'REACTIVATE';
    this.showConfirmModal = true;
  }

  closeConfirmModal() {
    this.showConfirmModal = false;
    this.confirmActionType = null;
  }

  executeConfirmAction() {
    if (!this.selectedSignId || !this.confirmActionType) return;

    if (this.confirmActionType === 'DELETE') {
      this.osmService.deleteTrafficSign(this.selectedSignId).subscribe({
        next: () => {
          this.zone.run(() => {
            this.loadTrafficSigns();
            this.closeSidebar();
            this.closeConfirmModal();
            this.cdr.detectChanges();
          });
        },
        error: (err) => { alert('Delete failed: ' + err.error?.message); this.closeConfirmModal(); }
      });
    } else if (this.confirmActionType === 'REACTIVATE') {
      this.osmService.reactivateTrafficSign(this.selectedSignId).subscribe({
        next: () => {
          this.zone.run(() => {
            this.loadTrafficSigns();
            this.closeSidebar();
            this.closeConfirmModal();
            this.cdr.detectChanges();
          });
        },
        error: (err) => { alert('Restore failed: ' + err.error?.message); this.closeConfirmModal(); }
      });
    }
  }

  highlight(text: string, term: string): string {
    if (!term || !text) return text;
    const sanitizedTerm = term.replace(/[-\/\\^$*+?.()|[\]{}]/g, '\\$&');
    const re = new RegExp(`(${sanitizedTerm})`, 'gi');
    return text.replace(re, '<b class="text-blue-600 font-black">$1</b>');
  }

  resetWizard() {
    this.wizardStep = 'MAIN_CATEGORY';
    this.selectedTemplate = null;
    this.lanes = [];
    this.laneCount = 1;
    this.activeLaneIndex = 0;
    this.isDirectionSelected = false;
  }

  logout() {
    alert('LogOut...');
  }
}
