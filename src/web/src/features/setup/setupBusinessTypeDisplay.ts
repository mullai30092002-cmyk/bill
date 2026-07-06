import type { TranslationKey } from '../../i18n/translations';
import type { SetupBusinessType } from './setupChecklistTypes';

export interface SetupBusinessTypeOption {
  value: SetupBusinessType;
  labelKey: TranslationKey;
}

export const setupBusinessTypeOptions: SetupBusinessTypeOption[] = [
  { value: 'Restaurant', labelKey: 'setupChecklist.businessTypeRestaurant' },
  { value: 'JuiceShop', labelKey: 'setupChecklist.businessTypeJuiceShop' },
  { value: 'Bakery', labelKey: 'setupChecklist.businessTypeBakery' },
  { value: 'DessertShop', labelKey: 'setupChecklist.businessTypeDessertShop' },
  { value: 'CafeTakeaway', labelKey: 'setupChecklist.businessTypeCafeTakeaway' },
];

export const getSetupBusinessTypeLabelKey = (businessType: SetupBusinessType): TranslationKey => {
  switch (businessType) {
    case 'JuiceShop':
      return 'setupChecklist.businessTypeJuiceShop';
    case 'Bakery':
      return 'setupChecklist.businessTypeBakery';
    case 'DessertShop':
      return 'setupChecklist.businessTypeDessertShop';
    case 'CafeTakeaway':
      return 'setupChecklist.businessTypeCafeTakeaway';
    default:
      return 'setupChecklist.businessTypeRestaurant';
  }
};

export const getSetupBusinessTypeGuidanceKey = (businessType: SetupBusinessType): TranslationKey => {
  switch (businessType) {
    case 'JuiceShop':
      return 'setupChecklist.businessTypeGuidanceJuiceShop';
    case 'Bakery':
      return 'setupChecklist.businessTypeGuidanceBakery';
    case 'DessertShop':
      return 'setupChecklist.businessTypeGuidanceDessertShop';
    case 'CafeTakeaway':
      return 'setupChecklist.businessTypeGuidanceCafeTakeaway';
    default:
      return 'setupChecklist.businessTypeGuidanceRestaurant';
  }
};
