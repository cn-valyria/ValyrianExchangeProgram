import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { CalendarDate } from '@internationalized/date'
import {
  UCalendar,
  UInputMenu,
  UInputNumber,
  URadioGroup,
  USelect,
  USelectMenu,
} from '#components'
import AccountEditDialog from '~/components/admin/AccountEditDialog.vue'
import AccountSlotsEditor from '~/components/admin/AccountSlotsEditor.vue'
import AdminAccessSettingsDialog from '~/components/admin/AdminAccessSettingsDialog.vue'
import AdminTeamEventDialog from '~/components/admin/AdminTeamEventDialog.vue'
import AidActivityFilter from '~/components/admin/AidActivityFilter.vue'
import CancelDealDialog from '~/components/admin/CancelDealDialog.vue'
import CreateDealDialog from '~/components/admin/CreateDealDialog.vue'
import DealSlotPicker from '~/components/admin/DealSlotPicker.vue'
import ResolveFlagDialog from '~/components/admin/ResolveFlagDialog.vue'
import { DealExtensionKind } from '~/stores/account'
import { DealFlagType } from '~/stores/adminDeals'
import { cnToday, Team } from '~/infrastructure/teamEvents'
import {
  accountSimplified,
  adminAccount,
  adminDeal,
  apiError,
  apiOk,
  cancelledDealFlag,
  dealFlag,
  slot,
} from '../helpers/fixtures'
import {
  bodyText,
  button,
  buttons,
  click,
  inputByPlaceholder,
  mountInApp,
  toasts,
  type,
} from '../helpers/dom'

// Only AccountEditDialog calls the API directly; everything else goes through stores, which are spied on
const { apiFetch } = vi.hoisted(() => ({ apiFetch: vi.fn() }))
mockNuxtImport('useApiClient', () => () => ({ apiFetch }))

beforeEach(() => {
  vi.spyOn(console, 'error').mockImplementation(() => {})
  vi.spyOn(console, 'log').mockImplementation(() => {})
})

// Dialogs are opened through useOverlay in the app; open: true renders them the same way
const openDialog = (
  component: Parameters<typeof mountInApp>[0],
  props: Record<string, unknown> = {},
) => {
  const onClose = vi.fn()
  return mountInApp(component, { open: true, onClose, ...props }).then(
    (wrapper) => ({ wrapper, onClose }),
  )
}

describe('CancelDealDialog', () => {
  it('requires a reason, then cancels the deal with it', async () => {
    const cancelDeal = vi
      .spyOn(useAdminDealsStore(), 'cancelDeal')
      .mockResolvedValue()
    const { onClose } = await openDialog(CancelDealDialog, {
      deal: adminDeal({ id: 10 }),
    })

    expect(bodyText()).toContain('This ends the deal between Ruler1 and Ruler2')
    expect(button('Cancel Deal').disabled).toBe(true)
    await type(
      inputByPlaceholder('Why is this deal being cancelled?'),
      '  Partner left VEP  ',
    )
    await click(button('Cancel Deal'))

    expect(cancelDeal).toHaveBeenCalledWith(10, 'Partner left VEP')
    expect(toasts()[0]).toMatchObject({
      color: 'success',
      description: 'Deal #10 cancelled.',
    })
    expect(onClose).toHaveBeenCalledWith(true)
  })

  it('stays open with an error when cancelling fails', async () => {
    vi.spyOn(useAdminDealsStore(), 'cancelDeal').mockRejectedValue(
      new Error('down'),
    )
    const { onClose } = await openDialog(CancelDealDialog, {
      deal: adminDeal(),
    })

    await type(inputByPlaceholder('Why is this deal being cancelled?'), 'x')
    await click(button('Cancel Deal'))

    expect(toasts()[0]).toMatchObject({ color: 'error' })
    expect(onClose).not.toHaveBeenCalled()
  })

  it('closes without changes from Keep Deal', async () => {
    const cancelDeal = vi.spyOn(useAdminDealsStore(), 'cancelDeal')
    const { onClose } = await openDialog(CancelDealDialog, {
      deal: adminDeal(),
    })

    await click(button('Keep Deal'))

    expect(onClose).toHaveBeenCalledWith(false)
    expect(cancelDeal).not.toHaveBeenCalled()
  })
})

describe('ResolveFlagDialog', () => {
  const radios = () =>
    Array.from(
      document.querySelectorAll<HTMLElement>('[role="dialog"] [role="radio"]'),
    )

  it('makes the admin pick an option, then clears the flag with the notes', async () => {
    const resolveFlag = vi
      .spyOn(useAdminDealsStore(), 'resolveFlag')
      .mockResolvedValue()
    const { onClose } = await openDialog(ResolveFlagDialog, {
      flag: dealFlag({ id: 5, dealId: 10 }),
    })

    expect(bodyText()).toContain('Resolve Flag on Deal #10')
    expect(bodyText()).toContain(
      '1-Day Warning against Ruler1 · Active, still held up',
    )
    expect(bodyText()).toContain("This doesn't stop the clock")
    expect(button('Resolve').disabled).toBe(true)
    await click(radios()[2])
    await type(
      inputByPlaceholder('Optional notes on how this was resolved'),
      ' Partner was on vacation ',
    )
    await click(button('Clear Flag'))

    expect(resolveFlag).toHaveBeenCalledWith(5, 'Partner was on vacation')
    expect(toasts()[0]).toMatchObject({
      color: 'success',
      description: 'Flag cleared.',
    })
    expect(onClose).toHaveBeenCalledWith(true)
  })

  it('cancels a held-up deal with a reason, then clears the warning', async () => {
    const store = useAdminDealsStore()
    const cancelDeal = vi.spyOn(store, 'cancelDeal').mockResolvedValue()
    const resolveFlag = vi.spyOn(store, 'resolveFlag').mockResolvedValue()
    const { onClose } = await openDialog(ResolveFlagDialog, {
      flag: dealFlag({ id: 5, dealId: 10 }),
    })

    await click(radios()[3])
    expect(button('Cancel Deal').disabled).toBe(true)
    await type(
      inputByPlaceholder('Why is this deal being cancelled?'),
      'Inactive for a week',
    )
    await click(button('Cancel Deal'))

    expect(cancelDeal).toHaveBeenCalledWith(10, 'Inactive for a week')
    expect(resolveFlag).toHaveBeenCalledWith(5, 'Inactive for a week')
    expect(onClose).toHaveBeenCalledWith(true)
  })

  it.each([
    [
      0,
      DealExtensionKind.FiveDays,
      'Give 5 More Days',
      'Ruler1 now has until 2026-10-18 on deal #10.',
    ],
    [
      1,
      DealExtensionKind.UntilNextLeg,
      'Hold Off Until Next Aid',
      "Deal #10 won't be cancelled before its next aid is recorded.",
    ],
  ])(
    'gives a held-up deal more time (option %i)',
    async (index, kind, confirm, message) => {
      const extend = vi
        .spyOn(useAdminDealsStore(), 'extendFlaggedDeal')
        .mockResolvedValue()
      const { onClose } = await openDialog(ResolveFlagDialog, {
        flag: dealFlag({ id: 5, dealId: 10 }),
      })

      await click(radios()[index])
      await type(
        inputByPlaceholder('Optional notes on how this was resolved'),
        'On vacation',
      )
      await click(button(confirm))

      expect(extend).toHaveBeenCalledWith(5, kind, 'On vacation')
      expect(toasts()[0]).toMatchObject({ description: message })
      expect(onClose).toHaveBeenCalledWith(true)
    },
  )

  it('restores a deal cancelled by its flag', async () => {
    const restore = vi
      .spyOn(useAdminDealsStore(), 'restoreFlaggedDeal')
      .mockResolvedValue()
    const { onClose } = await openDialog(ResolveFlagDialog, {
      flag: cancelledDealFlag({ id: 5, dealId: 10 }),
    })

    await click(radios()[0])
    await click(button('Restore Deal'))

    expect(restore).toHaveBeenCalledWith(5, '')
    expect(toasts()[0]).toMatchObject({ description: 'Deal #10 restored.' })
    expect(onClose).toHaveBeenCalledWith(true)
  })

  it("explains why a deal can't be restored, and won't select it", async () => {
    await openDialog(ResolveFlagDialog, {
      flag: cancelledDealFlag({
        innocentSlotOtherDealId: 31,
        canRestoreDeal: false,
      }),
    })

    expect(bodyText()).toContain(
      "Not available: Ruler2's slot #1 has been matched into deal #31 since.",
    )
    expect(radios()[0].hasAttribute('disabled')).toBe(true)
  })

  it("clears both accounts' flags for failed aid", async () => {
    const resolveFlag = vi
      .spyOn(useAdminDealsStore(), 'resolveFlag')
      .mockResolvedValue()
    await openDialog(ResolveFlagDialog, {
      flag: cancelledDealFlag({ id: 5, flagType: DealFlagType.AidFailed }),
      partnerFlag: cancelledDealFlag({
        id: 6,
        flagType: DealFlagType.AidFailed,
        flaggedAccount: accountSimplified(2),
        innocentAccount: accountSimplified(1),
      }),
    })

    await click(radios()[2])
    await click(button('Clear Both Flags'))

    expect(resolveFlag).toHaveBeenNthCalledWith(1, 5, '')
    expect(resolveFlag).toHaveBeenNthCalledWith(2, 6, '')
    expect(toasts()[0]).toMatchObject({ description: 'Both flags cleared.' })
  })

  it('stays open with an error when resolving fails', async () => {
    vi.spyOn(useAdminDealsStore(), 'resolveFlag').mockRejectedValue(
      new Error('down'),
    )
    const { onClose } = await openDialog(ResolveFlagDialog, {
      flag: dealFlag(),
    })

    await click(radios()[2])
    await click(button('Clear Flag'))

    expect(toasts()[0]).toMatchObject({ color: 'error' })
    expect(onClose).not.toHaveBeenCalled()
  })

  it('says to reload when the deal can no longer be restored', async () => {
    vi.spyOn(useAdminDealsStore(), 'restoreFlaggedDeal').mockRejectedValue({
      statusCode: 409,
      message: 'Conflict',
    })
    const { onClose } = await openDialog(ResolveFlagDialog, {
      flag: cancelledDealFlag(),
    })

    await click(radios()[0])
    await click(button('Restore Deal'))

    expect(toasts()[0]).toMatchObject({
      color: 'error',
      description:
        "This deal has changed since the page loaded, so that can't be done any more. Reload the page to see why.",
    })
    expect(onClose).not.toHaveBeenCalled()
  })

  it('closes without changes from Cancel', async () => {
    const { onClose } = await openDialog(ResolveFlagDialog, {
      flag: dealFlag(),
    })

    await click(button('Cancel'))

    expect(onClose).toHaveBeenCalledWith(false)
  })
})

describe('DealSlotPicker', () => {
  const accounts = [
    adminAccount(1),
    adminAccount(2, { rulerName: 'Sue', nationName: 'Sellland' }),
  ]

  it("loads the chosen account's open slots, and picks one with its account attached", async () => {
    const loadSlots = vi
      .spyOn(useAdminDealsStore(), 'loadAvailableSlots')
      .mockResolvedValue([
        slot(
          7,
          { tech: 1 },
          { slotNumber: 3, cashReceivedCycles: 1, account: undefined as never },
        ),
      ])
    const onUpdate = vi.fn()
    const wrapper = await mountInApp(DealSlotPicker, {
      label: 'First Account',
      accounts,
      'onUpdate:modelValue': onUpdate,
    })

    const accountSelect = wrapper.findComponent(USelectMenu)
    expect(
      (accountSelect.props('items') as { label: string }[]).map((i) => i.label),
    ).toEqual(['Ruler1 of Nation1', 'Sue of Sellland'])
    accountSelect.vm.$emit('update:modelValue', 2)
    await flushPromises()

    expect(loadSlots).toHaveBeenCalledWith(2)
    const slotSelect = wrapper.findAllComponents(USelectMenu)[1]
    expect((slotSelect.props('items') as { label: string }[])[0].label).toBe(
      'Slot #3: sends 100 tech, receives $9M',
    )
    slotSelect.vm.$emit('update:modelValue', 7)
    await flushPromises()

    expect(onUpdate).toHaveBeenLastCalledWith(
      expect.objectContaining({
        id: 7,
        account: {
          accountId: 2,
          nationId: 102,
          nationName: 'Sellland',
          rulerName: 'Sue',
          allianceName: 'Valyria',
        },
      }),
    )
  })

  it('says when the account has no open slots', async () => {
    vi.spyOn(useAdminDealsStore(), 'loadAvailableSlots').mockResolvedValue([])
    const wrapper = await mountInApp(DealSlotPicker, {
      label: 'First Account',
      accounts,
    })

    wrapper.findComponent(USelectMenu).vm.$emit('update:modelValue', 1)
    await flushPromises()

    expect(wrapper.text()).toContain('This account has no open slots.')
  })

  it("shows an error when the slots can't be loaded", async () => {
    vi.spyOn(useAdminDealsStore(), 'loadAvailableSlots').mockRejectedValue(
      new Error('down'),
    )
    const wrapper = await mountInApp(DealSlotPicker, {
      label: 'First Account',
      accounts,
    })

    wrapper.findComponent(USelectMenu).vm.$emit('update:modelValue', 1)
    await flushPromises()

    expect(toasts()[0]).toMatchObject({
      color: 'error',
      description: 'Failed to load open slots for that account.',
    })
  })

  it('clears the slot when the account is cleared', async () => {
    const loadSlots = vi
      .spyOn(useAdminDealsStore(), 'loadAvailableSlots')
      .mockResolvedValue([slot(7)])
    const onUpdate = vi.fn()
    const wrapper = await mountInApp(DealSlotPicker, {
      label: 'First Account',
      accounts,
      'onUpdate:modelValue': onUpdate,
    })
    wrapper.findComponent(USelectMenu).vm.$emit('update:modelValue', 1)
    await flushPromises()
    wrapper.findAllComponents(USelectMenu)[1].vm.$emit('update:modelValue', 7)
    await flushPromises()

    wrapper.findComponent(USelectMenu).vm.$emit('update:modelValue', undefined)
    await flushPromises()

    expect(loadSlots).toHaveBeenCalledOnce()
    expect(onUpdate).toHaveBeenLastCalledWith(null)
  })
})

describe('CreateDealDialog', () => {
  const buyerSlot = slot(
    1,
    { cash: 1 },
    {
      techReceivedCycles: 1,
      account: {
        accountId: 1,
        nationId: 101,
        nationName: 'Buyland',
        rulerName: 'Bob',
        allianceName: 'V',
      },
    },
  )
  const sellerSlot = slot(
    2,
    { tech: 1 },
    {
      cashReceivedCycles: 1,
      account: {
        accountId: 2,
        nationId: 102,
        nationName: 'Sellland',
        rulerName: 'Sue',
        allianceName: 'V',
      },
    },
  )

  const pick = async (
    wrapper: Awaited<ReturnType<typeof mountInApp>>,
    first: unknown,
    second: unknown,
  ) => {
    const pickers = wrapper.findAllComponents(DealSlotPicker)
    pickers[0].vm.$emit('update:modelValue', first)
    pickers[1].vm.$emit('update:modelValue', second)
    await flushPromises()
  }

  it('creates a deal between two matching slots', async () => {
    const createDeal = vi
      .spyOn(useAdminDealsStore(), 'createDeal')
      .mockResolvedValue()
    const { wrapper, onClose } = await openDialog(CreateDealDialog, {
      accounts: [adminAccount(1), adminAccount(2)],
    })
    expect(button('Create Deal').disabled).toBe(true)

    await pick(wrapper, buyerSlot, sellerSlot)

    expect(bodyText()).toContain('Bob sends $9M; Sue sends 100 tech')
    await click(button('Create Deal'))
    expect(createDeal).toHaveBeenCalledWith(1, 2)
    expect(toasts()[0]).toMatchObject({
      color: 'success',
      description: 'Deal created.',
    })
    expect(onClose).toHaveBeenCalledWith(true)
  })

  it("warns when the slots don't line up, but still allows the deal", async () => {
    const { wrapper } = await openDialog(CreateDealDialog, { accounts: [] })

    await pick(wrapper, buyerSlot, { ...sellerSlot, techSentCycles: 2 })

    expect(bodyText()).toContain("These slots aren't an exact match")
    expect(button('Create Deal').disabled).toBe(false)
  })

  it('refuses a deal between two slots of the same account', async () => {
    const { wrapper } = await openDialog(CreateDealDialog, { accounts: [] })

    await pick(wrapper, buyerSlot, {
      ...sellerSlot,
      account: buyerSlot.account,
    })

    expect(bodyText()).toContain('Both slots belong to the same account')
    expect(button('Create Deal').disabled).toBe(true)
  })

  it('stays open with an error when creating fails', async () => {
    vi.spyOn(useAdminDealsStore(), 'createDeal').mockRejectedValue(
      new Error('duplicate'),
    )
    const { wrapper, onClose } = await openDialog(CreateDealDialog, {
      accounts: [],
    })
    await pick(wrapper, buyerSlot, sellerSlot)

    await click(button('Create Deal'))

    expect(toasts()[0]).toMatchObject({ color: 'error' })
    expect(onClose).not.toHaveBeenCalled()
  })

  it('closes without changes from Cancel', async () => {
    const { onClose } = await openDialog(CreateDealDialog, { accounts: [] })

    await click(button('Cancel'))

    expect(onClose).toHaveBeenCalledWith(false)
  })
})

describe('AccountSlotsEditor', () => {
  const slots = () => [
    slot(1, { cash: 1 }, { slotNumber: 1 }),
    slot(2, { cash: 1 }, { slotNumber: 2, hasActiveDeal: false }),
  ]

  it('lists the slots, and only saves one that was changed', async () => {
    const store = useAdminAccountsStore()
    vi.spyOn(store, 'loadAccountSlots').mockResolvedValue(slots())
    const updateSlot = vi.spyOn(store, 'updateSlot').mockResolvedValue()
    const wrapper = await mountInApp(AccountSlotsEditor, { accountId: 4 })

    expect(store.loadAccountSlots).toHaveBeenCalledWith(4)
    expect(wrapper.text()).toContain('Slot #1In a deal')
    expect(wrapper.text()).toContain('Slot #2Open')
    const saveButtons = () =>
      wrapper.findAll('button').filter((b) => b.text() === 'Save')
    expect(saveButtons().map((b) => b.attributes('disabled'))).toEqual(['', ''])

    wrapper.findAllComponents(UInputNumber)[4].vm.$emit('update:modelValue', 2)
    await flushPromises()
    expect(saveButtons()[1].attributes('disabled')).toBeUndefined()
    await click(saveButtons()[1].element as HTMLElement)

    expect(updateSlot).toHaveBeenCalledWith(
      expect.objectContaining({ id: 2, techSentCycles: 2 }),
    )
    expect(toasts()[0]).toMatchObject({
      color: 'success',
      description: 'Slot #2 updated.',
    })
    expect(saveButtons()[1].attributes('disabled')).toBe('')
  })

  it('keeps the change unsaved when saving fails', async () => {
    const store = useAdminAccountsStore()
    vi.spyOn(store, 'loadAccountSlots').mockResolvedValue(slots())
    vi.spyOn(store, 'updateSlot').mockRejectedValue(new Error('down'))
    const wrapper = await mountInApp(AccountSlotsEditor, { accountId: 4 })

    wrapper.findAllComponents(UInputNumber)[0].vm.$emit('update:modelValue', 3)
    await flushPromises()
    const save = wrapper.findAll('button').filter((b) => b.text() === 'Save')[0]
    await click(save.element as HTMLElement)

    expect(toasts()[0]).toMatchObject({ color: 'error' })
    expect(save.attributes('disabled')).toBeUndefined()
  })

  it('says when the account has no slots', async () => {
    vi.spyOn(useAdminAccountsStore(), 'loadAccountSlots').mockResolvedValue([])
    const wrapper = await mountInApp(AccountSlotsEditor, { accountId: 4 })

    expect(wrapper.text()).toContain('This account has no slots.')
  })

  it("shows an error when the slots can't be loaded", async () => {
    vi.spyOn(useAdminAccountsStore(), 'loadAccountSlots').mockRejectedValue(
      new Error('down'),
    )
    await mountInApp(AccountSlotsEditor, { accountId: 4 })

    expect(toasts()[0]).toMatchObject({
      color: 'error',
      description: "Failed to load this account's slots.",
    })
  })
})

describe('AccountEditDialog', () => {
  const prospect = {
    nationId: 123,
    nationName: 'Newland',
    rulerName: 'Nia',
    allianceName: 'Valyria',
  }

  describe('creating an account', () => {
    it('finds the nation, then creates the account', async () => {
      apiFetch.mockResolvedValue(apiOk(prospect))
      const createAccount = vi
        .spyOn(useAdminAccountsStore(), 'createAccount')
        .mockResolvedValue()
      const { wrapper, onClose } = await openDialog(AccountEditDialog, {
        account: {},
      })

      expect(bodyText()).toContain('New Account')
      expect(bodyText()).not.toContain('Secret Code')
      await type(inputByPlaceholder("The account's NationID"), '123')
      await click(button('Search'))

      expect(apiFetch).toHaveBeenCalledWith(
        '/api/accounts/prospect?nationId=123',
      )
      expect(bodyText()).toContain('Secret Code')
      wrapper.findComponent(USelectMenu).vm.$emit('update:modelValue', 'S')
      // FAC and DRA have to be picked explicitly today (see the skipped test below)
      const [fm, fac, dra] = wrapper.findAllComponents(URadioGroup)
      fm.vm.$emit('update:modelValue', 'Y')
      fac.vm.$emit('update:modelValue', 'N')
      dra.vm.$emit('update:modelValue', 'N')
      await click(button('Generate'))
      await type(
        inputByPlaceholder('e.g. 318737615257993226'),
        '318737615257993226',
      )
      await click(button('Create'))

      expect(createAccount).toHaveBeenCalledOnce()
      const payload = createAccount.mock.calls[0][0]
      expect(payload).toMatchObject({
        nationId: 123,
        rulerName: 'Nia',
        role: 'S',
        hasForeignMinistry: true,
        hasFederalAidCommission: false,
        hasDisasterReliefAgency: false,
        discord: null,
        discordUniqueId: 318737615257993226n,
      })
      expect(payload.uniqueCode).toMatch(/^\d{5}$/)
      expect(toasts()[0]).toMatchObject({
        color: 'success',
        description: 'Account created successfully!',
      })
      expect(onClose).toHaveBeenCalledWith(false)
    })

    it('stays open with an error when creating fails', async () => {
      apiFetch.mockResolvedValue(apiOk(prospect))
      vi.spyOn(useAdminAccountsStore(), 'createAccount').mockRejectedValue(
        new Error('down'),
      )
      const { wrapper, onClose } = await openDialog(AccountEditDialog, {
        account: {},
      })
      await type(inputByPlaceholder("The account's NationID"), '123')
      await click(button('Search'))
      wrapper
        .findAllComponents(URadioGroup)
        .forEach((radio) => radio.vm.$emit('update:modelValue', 'N'))
      await click(button('Generate'))

      await click(button('Create'))

      expect(toasts()[0]).toMatchObject({ color: 'error' })
      expect(onClose).not.toHaveBeenCalled()
    })

    it('creates an account with the improvements left at their default of No', async () => {
      apiFetch.mockResolvedValue(apiOk(prospect))
      const createAccount = vi
        .spyOn(useAdminAccountsStore(), 'createAccount')
        .mockResolvedValue()
      const { wrapper } = await openDialog(AccountEditDialog, { account: {} })
      await type(inputByPlaceholder("The account's NationID"), '123')
      await click(button('Search'))
      wrapper.findComponent(USelectMenu).vm.$emit('update:modelValue', 'B')
      await click(button('Generate'))

      await click(button('Create'))

      expect(createAccount).toHaveBeenCalledWith(
        expect.objectContaining({
          hasForeignMinistry: false,
          hasFederalAidCommission: false,
          hasDisasterReliefAgency: false,
        }),
      )
    })

    it.each([
      [404, 'No nation was found with that nation ID.'],
      [409, 'That nation already has a VEP account.'],
      [
        500,
        'VEP encountered an error while searching for the above nation ID.',
      ],
    ])(
      'explains a failed nation search (%i) and lets the admin try again',
      async (statusCode, message) => {
        apiFetch.mockResolvedValue(apiError({ statusCode, message: 'Failed' }))
        await openDialog(AccountEditDialog, { account: {} })
        await type(inputByPlaceholder("The account's NationID"), '123')

        await click(button('Search'))

        expect(toasts()[0]).toMatchObject({ color: 'error' })
        expect(toasts()[0].description).toContain(message)
        expect(button('Search').disabled).toBe(false)
        expect(bodyText()).not.toContain('Secret Code')
      },
    )
  })

  describe('editing an account', () => {
    // The slots editor has its own Save buttons; the dialog's is last, in the footer
    const saveAccount = () => click(buttons('Save').at(-1)!)

    beforeEach(() => {
      vi.spyOn(useAdminAccountsStore(), 'loadAccountSlots').mockResolvedValue([
        slot(1),
      ])
    })

    it('shows the account with its slots, and saves changes', async () => {
      const updateAccount = vi
        .spyOn(useAdminAccountsStore(), 'updateAccount')
        .mockResolvedValue()
      const { wrapper, onClose } = await openDialog(AccountEditDialog, {
        account: adminAccount(1, { discord: 'old', discordUniqueId: 5n }),
      })

      expect(bodyText()).toContain("Ruler1's Account")
      expect(bodyText()).toContain('(1 / 4 in deals)')
      expect(useAdminAccountsStore().loadAccountSlots).toHaveBeenCalledWith(1)
      await type(inputByPlaceholder('e.g. username or username#1234'), '')
      await type(inputByPlaceholder('e.g. 318737615257993226'), 'not a number')
      wrapper
        .findAllComponents(URadioGroup)[2]
        .vm.$emit('update:modelValue', 'Y')
      await saveAccount()

      expect(updateAccount).toHaveBeenCalledWith(
        expect.objectContaining({
          id: 1,
          discord: null,
          discordUniqueId: null,
          hasDisasterReliefAgency: true,
        }),
      )
      expect(toasts()[0]).toMatchObject({
        color: 'success',
        description: 'Account updated successfully!',
      })
      expect(onClose).toHaveBeenCalledWith(false)
    })

    it('stays open with an error when saving fails', async () => {
      vi.spyOn(useAdminAccountsStore(), 'updateAccount').mockRejectedValue(
        new Error('down'),
      )
      const { onClose } = await openDialog(AccountEditDialog, {
        account: adminAccount(1),
      })

      await saveAccount()

      expect(toasts()[0]).toMatchObject({ color: 'error' })
      expect(onClose).not.toHaveBeenCalled()
    })

    it('rejects an empty unique code', async () => {
      const updateAccount = vi.spyOn(useAdminAccountsStore(), 'updateAccount')
      await openDialog(AccountEditDialog, { account: adminAccount(1) })

      const uniqueCode = Array.from(
        document.querySelectorAll<HTMLInputElement>('input'),
      ).find((input) => input.value === '12345')!
      await type(uniqueCode, '')
      await saveAccount()

      expect(updateAccount).not.toHaveBeenCalled()
      expect(toasts()[0]).toMatchObject({
        title: 'Validation Error',
        color: 'error',
      })
      expect(toasts()[0].description).toContain(
        'Every account must have a unique code provided',
      )
    })

    it('closes without saving from Cancel', async () => {
      const updateAccount = vi.spyOn(useAdminAccountsStore(), 'updateAccount')
      const { onClose } = await openDialog(AccountEditDialog, {
        account: adminAccount(1),
      })

      await click(button('Cancel'))

      expect(onClose).toHaveBeenCalledWith(false)
      expect(updateAccount).not.toHaveBeenCalled()
    })

    it('leaves the account in the list alone when the admin cancels', async () => {
      const store = useAdminAccountsStore()
      store.accounts = [adminAccount(1, { discord: 'old' })]
      await openDialog(AccountEditDialog, { account: store.accounts[0] })

      await type(
        inputByPlaceholder('e.g. username or username#1234'),
        'changed',
      )
      await click(button('Cancel'))

      expect(store.accounts[0].discord).toBe('old')
    })
  })
})

describe('AdminAccessSettingsDialog', () => {
  it('loads accounts to choose from, then grants the chosen role', async () => {
    const accountsStore = useAdminAccountsStore()
    vi.spyOn(accountsStore, 'loadAllAccounts').mockImplementation(async () => {
      accountsStore.accounts = [adminAccount(1)]
    })
    const createAccess = vi
      .spyOn(useAdminAccessStore(), 'createAdminAccess')
      .mockResolvedValue(true)
    const { wrapper, onClose } = await openDialog(AdminAccessSettingsDialog, {
      editingAdmin: {},
    })

    expect(accountsStore.loadAllAccounts).toHaveBeenCalledWith(false)
    expect(bodyText()).toContain('Add Admin Access')
    wrapper.findComponent(UInputMenu).vm.$emit('update:modelValue', 1)
    wrapper.findComponent(USelectMenu).vm.$emit('update:modelValue', 3)
    await flushPromises()
    await click(button('Save'))

    expect(createAccess).toHaveBeenCalledWith(
      expect.objectContaining({ accountId: 1, adminRoleId: 3 }),
    )
    expect(toasts()[0]).toMatchObject({
      color: 'success',
      description: 'Access added successfully!',
    })
    expect(onClose).toHaveBeenCalledWith(false)
  })

  it("doesn't reload accounts that are already loaded", async () => {
    const accountsStore = useAdminAccountsStore()
    accountsStore.accounts = [adminAccount(1)]
    const loadAll = vi.spyOn(accountsStore, 'loadAllAccounts')

    await openDialog(AdminAccessSettingsDialog, {
      editingAdmin: { accountId: 1, adminRoleId: 1 },
    })

    expect(loadAll).not.toHaveBeenCalled()
    expect(bodyText()).toContain('Edit Admin Access')
  })

  it("shows an error when accounts can't be loaded", async () => {
    vi.spyOn(useAdminAccountsStore(), 'loadAllAccounts').mockRejectedValue(
      new Error('down'),
    )

    await openDialog(AdminAccessSettingsDialog, { editingAdmin: {} })

    expect(toasts()[0]).toMatchObject({
      color: 'error',
      description: 'Failed to load accounts. Please try again.',
    })
  })

  it('closes without saving from Cancel', async () => {
    useAdminAccountsStore().accounts = [adminAccount(1)]
    const { onClose } = await openDialog(AdminAccessSettingsDialog, {
      editingAdmin: {},
    })

    await click(button('Cancel'))

    expect(onClose).toHaveBeenCalledWith(false)
  })

  it('stays open with an error when the grant fails', async () => {
    useAdminAccountsStore().accounts = [adminAccount(1)]
    vi.spyOn(useAdminAccessStore(), 'createAdminAccess').mockResolvedValue(
      false,
    )
    const { onClose } = await openDialog(AdminAccessSettingsDialog, {
      editingAdmin: { accountId: 1, adminRoleId: 1 },
    })

    await click(button('Save'))

    expect(toasts()[0]).toMatchObject({ color: 'error' })
    expect(onClose).not.toHaveBeenCalled()
  })
})

describe('AdminTeamEventDialog', () => {
  const pickTeamAndDate = async (
    wrapper: Awaited<ReturnType<typeof openDialog>>['wrapper'],
  ) => {
    wrapper.findComponent(USelect).vm.$emit('update:modelValue', Team.Aqua)
    await click(button('Pick a date'))
    wrapper
      .findComponent(UCalendar)
      .vm.$emit('update:modelValue', new CalendarDate(2026, 10, 6))
    await flushPromises()
  }

  it('explains when to add an event, and offers only teams with a senate', async () => {
    const { wrapper } = await openDialog(AdminTeamEventDialog)

    expect(bodyText()).toContain(
      "Wait until a majority of the team's senators have approved the proposal",
    )
    const teams = wrapper.findComponent(USelect).props('items') as {
      value: Team
    }[]
    expect(teams).toHaveLength(12)
    expect(teams.map((t) => t.value)).not.toContain(Team.None)
    expect(button('Add Event').disabled).toBe(true)
  })

  it("doesn't allow approval dates after today's CN date", async () => {
    const { wrapper } = await openDialog(AdminTeamEventDialog)

    await click(button('Pick a date'))

    expect(String(wrapper.findComponent(UCalendar).props('maxValue'))).toBe(
      cnToday(),
    )
  })

  it('previews when the bonus slot ends and stops being matched, then adds the event', async () => {
    const createTeamEvent = vi
      .spyOn(useAdminTeamEventsStore(), 'createTeamEvent')
      .mockResolvedValue()
    const { wrapper, onClose } = await openDialog(AdminTeamEventDialog)

    await pickTeamAndDate(wrapper)

    expect(bodyText()).toContain('in effect through 11/5/2026')
    expect(bodyText()).toContain(
      'after 10/16/2026 for 1:2 slots and 10/26/2026 for 1:1 slots',
    )
    await click(button('Add Event'))

    expect(createTeamEvent).toHaveBeenCalledWith({
      team: Team.Aqua,
      approvedOn: '2026-10-06',
    })
    expect(toasts()[0]).toMatchObject({ color: 'success' })
    expect(onClose).toHaveBeenCalledWith(true)
  })

  it.each([
    [409, 'This team already has a +1 aid slot event during those 30 days.'],
    [500, 'VEP encountered an error while saving'],
  ])(
    'stays open with an explanation when the API returns %i',
    async (statusCode, message) => {
      vi.spyOn(useAdminTeamEventsStore(), 'createTeamEvent').mockRejectedValue({
        statusCode,
        message: 'nope',
      })
      const { wrapper, onClose } = await openDialog(AdminTeamEventDialog)

      await pickTeamAndDate(wrapper)
      await click(button('Add Event'))

      expect(toasts()[0].color).toBe('error')
      expect(toasts()[0].description).toContain(message)
      expect(onClose).not.toHaveBeenCalled()
    },
  )

  it('closes without saving from Cancel', async () => {
    const createTeamEvent = vi.spyOn(
      useAdminTeamEventsStore(),
      'createTeamEvent',
    )
    const { onClose } = await openDialog(AdminTeamEventDialog)

    await click(button('Cancel'))

    expect(onClose).toHaveBeenCalledWith(false)
    expect(createTeamEvent).not.toHaveBeenCalled()
  })
})

describe('AidActivityFilter', () => {
  it('applies search text, direction and a date range', async () => {
    const store = useAdminAidActivityStore()
    const { wrapper, onClose } = await openDialog(AidActivityFilter, {
      filters: {},
    })
    const direction = () => wrapper.findComponent(USelect)

    expect(direction().props('disabled')).toBe(true)
    await type(inputByPlaceholder('Enter name to search...'), '  Bob  ')
    expect(direction().props('disabled')).toBe(false)
    direction().vm.$emit('update:modelValue', 'received')
    await click(button('Pick a date'))
    wrapper.findComponent(UCalendar).vm.$emit('update:modelValue', {
      start: new CalendarDate(2026, 10, 1),
      end: new CalendarDate(2026, 10, 9),
    })
    await flushPromises()
    expect(bodyText()).toContain('Oct 1, 2026 - Oct 9, 2026')
    await click(button('Apply Filters'))

    expect(store.filters).toEqual({
      search: 'Bob',
      direction: 'received',
      sentSince: new Date(2026, 9, 1),
      sentUntil: new Date(2026, 9, 9),
    })
    expect(onClose).toHaveBeenCalled()
  })

  it('searches a single day when only a start date is picked', async () => {
    const store = useAdminAidActivityStore()
    const { wrapper } = await openDialog(AidActivityFilter, { filters: {} })

    await click(button('Pick a date'))
    wrapper.findComponent(UCalendar).vm.$emit('update:modelValue', {
      start: new CalendarDate(2026, 10, 1),
      end: undefined,
    })
    await flushPromises()
    expect(bodyText()).toContain('Oct 1, 2026')
    await click(button('Apply Filters'))

    expect(store.filters).toEqual({
      search: undefined,
      direction: undefined,
      sentSince: new Date(2026, 9, 1),
      sentUntil: new Date(2026, 9, 1),
    })
  })

  it('starts from the current filters, and clears them', async () => {
    const store = useAdminAidActivityStore()
    const { wrapper } = await openDialog(AidActivityFilter, {
      filters: {
        search: 'Sue',
        direction: 'sent',
        sentSince: new Date(2026, 9, 1),
        sentUntil: new Date(2026, 9, 3),
      },
    })

    expect(inputByPlaceholder('Enter name to search...').value).toBe('Sue')
    expect(wrapper.findComponent(USelect).props('modelValue')).toBe('sent')
    expect(bodyText()).toContain('Oct 1, 2026 - Oct 3, 2026')

    await click(button('Clear'))
    expect(bodyText()).toContain('Pick a date')
    await click(button('Apply Filters'))

    expect(store.filters).toEqual({
      search: undefined,
      direction: undefined,
      sentSince: undefined,
      sentUntil: undefined,
    })
  })
})
